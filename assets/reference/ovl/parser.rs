use std::collections::HashMap;

use crate::error::{OvlError, OvlResult};
use crate::reader::{OvlData, OvlLoader, OvlLoaderEntry, le_u32};

#[derive(Debug, Default, Clone, Copy, PartialEq, Eq)]
pub enum OvlKind {
    Common,
    #[default]
    Unique,
}

#[derive(Debug, Clone)]
pub struct OvlResource {
    pub name: String,
    pub tag: String,
    pub address: u32,
    pub size: u32,
    pub kind: OvlKind,
    pub checksum: u32,
    pub entry: Option<usize>,
}

#[derive(Debug, Clone)]
pub struct OvlSymbolRef {
    pub reference_address: u32,
    pub symbol: String,
    pub kind: OvlKind,
}

#[derive(Debug, Default)]
pub struct OvlRelocation {
    pub source_kind: OvlKind,
    pub source_address: u32,
    pub source_file: u32,
    pub source_block: u32,
    pub target_address: u32,
    pub target_kind: OvlKind,
    pub target_file: u32,
    pub target_block: u32,
    pub is_symbol_ref: bool,
}

pub struct Ovl {
    pub id: String,
    pub common: OvlData,
    pub unique: OvlData,
    pub unique_offset: u32,
    pub relocations: Vec<OvlRelocation>,
    pub reloc_targets: HashMap<u32, u32>,
    pub resources: Vec<OvlResource>,
    pub symbol_refs: Vec<OvlSymbolRef>,
}

pub struct ResolvedAddress {
    pub kind: OvlKind,
    pub file: isize,
    pub block: usize,
    pub offset: usize,
}

impl Ovl {
    pub fn read(ovl_id: &str) -> OvlResult<Self> {
        let common = OvlData::read(ovl_id, true)?;
        let mut unique = OvlData::read(ovl_id, false)?;
        let unique_offset = common.rel_offset;

        for i in 0..9 {
            unique.file_blocks[i].rel_offset += unique_offset;
        }

        let mut ovl = Self {
            id: ovl_id.to_string(),
            common,
            unique,
            unique_offset,
            relocations: Vec::new(),
            reloc_targets: HashMap::new(),
            resources: Vec::new(),
            symbol_refs: Vec::new(),
        };
        ovl.resolve_all_relocations();
        ovl.extract_resources();
        ovl.extract_symbol_refs();
        Ok(ovl)
    }

    pub fn version(&self) -> u32 {
        self.common.version
    }

    pub fn side(&self, kind: OvlKind) -> &OvlData {
        match kind {
            OvlKind::Common => &self.common,
            OvlKind::Unique => &self.unique,
        }
    }

    pub fn resources(&self) -> &[OvlResource] {
        &self.resources
    }

    pub fn resource(&self, name: &str) -> OvlResult<&OvlResource> {
        self.resources
            .iter()
            .find(|r| r.name == name)
            .ok_or_else(|| OvlError::BadResource(format!("resource not found: {name}")))
    }

    pub fn resource_at(&self, address: u32) -> OvlResult<&OvlResource> {
        self.resources
            .iter()
            .find(|r| r.address == address)
            .ok_or_else(|| OvlError::BadResource(format!("no resource at address {address:#x}")))
    }

    pub fn resources_with_tag<'a>(&'a self, tag: &'a str) -> impl Iterator<Item = &'a OvlResource> {
        self.resources.iter().filter(move |r| r.tag == tag)
    }

    pub fn loader_entries(&self, kind: OvlKind) -> &[OvlLoaderEntry] {
        &self.side(kind).entries
    }

    pub fn entry(&self, res: &OvlResource) -> Option<&OvlLoaderEntry> {
        res.entry.and_then(|i| self.side(res.kind).entries.get(i))
    }

    pub fn entry_loader(&self, res: &OvlResource) -> Option<&OvlLoader> {
        let entry = self.entry(res)?;
        self.side(res.kind).loader_for(entry)
    }

    pub fn extra_chunks(&self, res: &OvlResource) -> &[Vec<u8>] {
        let Some(entry) = self.entry(res) else {
            return &[];
        };
        self.side(res.kind)
            .extra_data
            .get(entry.extra_range.clone())
            .unwrap_or(&[])
    }

    pub fn resource_data(&self, res: &OvlResource) -> OvlResult<&[u8]> {
        self.data(res.address, res.size as usize)
    }

    pub fn data(&self, address: u32, size: usize) -> OvlResult<&[u8]> {
        let resolved = self.resolve_address(address)?;
        let block = self.get_block_data(&resolved)?;
        let start = resolved.offset;
        if start + size > block.len() {
            return Err(OvlError::BadResource(format!(
                "read of {size} bytes at {address:#x} exceeds block bounds"
            )));
        }
        Ok(&block[start..start + size])
    }

    pub fn data_to_block_end(&self, address: u32) -> OvlResult<&[u8]> {
        let resolved = self.resolve_address(address)?;
        let block = self.get_block_data(&resolved)?;
        if resolved.offset > block.len() {
            return Err(OvlError::BadResource(format!(
                "offset for address {address:#x} exceeds block length"
            )));
        }
        Ok(&block[resolved.offset..])
    }

    pub fn read_u32(&self, address: u32) -> OvlResult<u32> {
        let bytes = self.data(address, 4)?;
        Ok(u32::from_le_bytes(bytes.try_into().unwrap()))
    }

    pub fn read_ptr(&self, address: u32) -> OvlResult<Option<u32>> {
        Ok(match self.read_u32(address)? {
            0 => None,
            other => Some(other),
        })
    }

    pub fn read_clamped(&self, addr: u32, max: usize) -> OvlResult<Vec<u8>> {
        if let Ok(d) = self.data(addr, max) {
            return Ok(d.to_vec());
        }
        let tail = self.data_to_block_end(addr)?;
        let take = tail.len().min(max);
        Ok(tail[..take].to_vec())
    }

    pub fn read_string(&self, address: u32) -> OvlResult<String> {
        let resolved = self.resolve_address(address)?;
        let block = self.get_block_data(&resolved)?;
        let start = resolved.offset;
        if start > block.len() {
            return Err(OvlError::BadResource(format!(
                "string offset for address {address:#x} exceeds block length"
            )));
        }
        let end = block[start..]
            .iter()
            .position(|&b| b == 0)
            .map(|p| start + p)
            .unwrap_or(block.len());
        Ok(String::from_utf8_lossy(&block[start..end]).into_owned())
    }

    pub fn reloc_target(&self, source: u32) -> Option<u32> {
        self.reloc_targets.get(&source).copied()
    }

    fn resolve_address(&self, address: u32) -> OvlResult<ResolvedAddress> {
        let (data, kind) = if address >= self.unique_offset {
            (&self.unique, OvlKind::Unique)
        } else {
            (&self.common, OvlKind::Common)
        };

        let mut file_index: i32 = -1;

        for i in 0..9 {
            if address >= data.file_blocks[i].rel_offset {
                file_index = i as i32;
            }
        }
        if file_index < 0 {
            return Err(OvlError::BadResource(format!(
                "address {address:#x} does not map to any file block"
            )));
        }

        let block = &data.file_blocks[file_index as usize];
        let mut sub_offset = block.rel_offset;
        for i in 0..block.blocks.len() {
            let sub_size = block.blocks[i as usize].len() as u32;
            if address >= sub_offset && address < sub_offset + sub_size {
                let offset = address - sub_offset;
                return Ok(ResolvedAddress {
                    kind,
                    file: file_index as isize,
                    block: i,
                    offset: offset as usize,
                });
            }
            sub_offset += sub_size;
        }

        Err(OvlError::BadResource(format!(
            "address {address:#x} does not map to any sub-block"
        )))
    }

    fn get_block_data(&self, source: &ResolvedAddress) -> OvlResult<&[u8]> {
        if source.file < 0 || source.file >= 9 {
            return Err(OvlError::BadResource(format!(
                "resolved file index {} out of range",
                source.file
            )));
        }
        Ok(match source.kind {
            OvlKind::Common => {
                self.common.file_blocks[source.file as usize].blocks[source.block].as_slice()
            }
            OvlKind::Unique => {
                self.unique.file_blocks[source.file as usize].blocks[source.block].as_slice()
            }
        })
    }

    fn resolve_relocations(&mut self, kind: OvlKind) {
        let relocs: Vec<u32> = match kind {
            OvlKind::Common => self.common.relocations.clone(),
            OvlKind::Unique => self.unique.relocations.clone(),
        };
        for &reloc in &relocs {
            let mut entry = OvlRelocation::default();
            entry.source_kind = kind;
            entry.source_address = reloc;

            let Ok(source) = self.resolve_address(reloc) else {
                continue;
            };

            entry.source_file = source.file as u32;
            entry.source_block = source.block as u32;

            let source_data = self.get_block_data(&source);
            let target_addr = match source_data {
                Ok(d) if source.offset + 4 <= d.len() => {
                    u32::from_le_bytes(d[source.offset..source.offset + 4].try_into().unwrap())
                }
                _ => continue,
            };

            entry.target_address = target_addr;
            self.reloc_targets.insert(reloc, target_addr);

            let Ok(target) = self.resolve_address(target_addr) else {
                self.relocations.push(entry);
                continue;
            };

            entry.target_kind = target.kind;
            entry.target_file = target.file as u32;
            entry.target_block = target.block as u32;
            self.relocations.push(entry);
        }
    }

    fn resolve_all_relocations(&mut self) {
        self.resolve_relocations(OvlKind::Common);
        self.resolve_relocations(OvlKind::Unique);
    }

    fn extract_resources(&mut self) {
        let mut resources = Vec::new();

        for kind in [OvlKind::Common, OvlKind::Unique] {
            let (version, sym, entry_by_addr) = {
                let data = self.side(kind);
                if data.file_blocks[2].blocks.is_empty() {
                    continue;
                }
                let sym = data.file_blocks[2].blocks[0].clone();
                let entry_by_addr: HashMap<u32, usize> = data
                    .entries
                    .iter()
                    .enumerate()
                    .map(|(i, e)| (e.data_address, i))
                    .collect();
                (data.version, sym, entry_by_addr)
            };

            if sym.is_empty() {
                continue;
            }

            let Some((rec_size, prefix)) = Self::detect_symbol_layout(version, sym.len()) else {
                continue;
            };

            let count = (sym.len() - prefix) / rec_size;

            for n in 0..count {
                let off = prefix + n * rec_size;
                let name_ptr = le_u32(&sym, off);
                let data_ptr = le_u32(&sym, off + 4);
                let checksum = if rec_size == 16 {
                    le_u32(&sym, off + 12)
                } else {
                    0
                };

                let Ok(full_name) = self.read_string(name_ptr) else {
                    continue;
                };

                let Ok(resolved) = self.resolve_address(data_ptr) else {
                    continue;
                };
                let Ok(block) = self.get_block_data(&resolved) else {
                    continue;
                };
                let size = (block.len() - resolved.offset) as u32;

                let (name, tag) = match full_name.rsplit_once(':') {
                    Some((n, t)) => (n.to_string(), t.to_string()),
                    None => (full_name, String::new()),
                };

                resources.push(OvlResource {
                    name,
                    tag,
                    address: data_ptr,
                    size,
                    kind,
                    checksum,
                    entry: entry_by_addr.get(&data_ptr).copied(),
                });
            }
        }

        self.clamp_resource_sizes(&mut resources);
        self.resources = resources;
    }

    fn extract_symbol_refs(&mut self) {
        let mut refs = Vec::new();
        for kind in [OvlKind::Common, OvlKind::Unique] {
            let (version, block) = {
                let data = self.side(kind);
                let Some(block) = data.file_blocks[2].blocks.get(2) else {
                    continue;
                };
                (data.version, block.clone())
            };
            let rec_size = if version == 1 { 12 } else { 16 };
            for rec in block.chunks_exact(rec_size) {
                let reference_address = le_u32(rec, 0);
                let name_ptr = le_u32(rec, 4);
                let Ok(symbol) = self.read_string(name_ptr) else {
                    continue;
                };
                if symbol.is_empty() {
                    continue;
                }
                refs.push(OvlSymbolRef {
                    reference_address,
                    symbol,
                    kind,
                });
            }
        }
        self.symbol_refs = refs;
    }

    pub fn symbol_ref_at(&self, address: u32) -> Option<&OvlSymbolRef> {
        self.symbol_refs
            .iter()
            .find(|r| r.reference_address == address)
    }

    fn clamp_resource_sizes(&self, resources: &mut [OvlResource]) {
        let mut order: Vec<usize> = (0..resources.len()).collect();
        order.sort_by_key(|&i| resources[i].address);

        for w in order.windows(2) {
            let cur_addr = resources[w[0]].address;
            let next_addr = resources[w[1]].address;
            if next_addr <= cur_addr {
                continue;
            }
            let gap = next_addr - cur_addr;
            if gap >= resources[w[0]].size {
                continue;
            }
            let (Ok(a), Ok(b)) = (
                self.resolve_address(cur_addr),
                self.resolve_address(next_addr),
            ) else {
                continue;
            };
            if a.kind == b.kind && a.file == b.file && a.block == b.block {
                resources[w[0]].size = gap;
            }
        }
    }

    fn detect_symbol_layout(version: u32, size: usize) -> Option<(usize, usize)> {
        let candidate = if version == 1 { 12 } else { 16 };
        if size % candidate == 0 {
            return Some((candidate, 0));
        }
        if size > 4 && (size - 4) % candidate == 0 {
            return Some((candidate, 4));
        }
        let other = if candidate == 12 { 16 } else { 12 };
        if size % other == 0 {
            return Some((other, 0));
        }
        if size > 4 && (size - 4) % other == 0 {
            return Some((other, 4));
        }
        None
    }

    pub fn extra_chunks_for(&self, kind: OvlKind, entry: &OvlLoaderEntry) -> &[Vec<u8>] {
        self.side(kind)
            .extra_data
            .get(entry.extra_range.clone())
            .unwrap_or(&[])
    }
}
