use notate::cursors::byte_cursor::{ByteCursor, ByteCursorError, ByteCursorResult};

use crate::paths::get_full_path;

pub fn le_u16(b: &[u8], o: usize) -> u16 {
    u16::from_le_bytes(b[o..o + 2].try_into().unwrap())
}

pub fn le_u32(b: &[u8], o: usize) -> u32 {
    u32::from_le_bytes(b[o..o + 4].try_into().unwrap())
}

pub fn le_i32(b: &[u8], o: usize) -> i32 {
    i32::from_le_bytes(b[o..o + 4].try_into().unwrap())
}

#[derive(Clone)]
pub struct OvlLoader {
    pub category: String,
    pub name: String,
    pub variant: u32,
    pub tag: String,
    pub symbols: u32,
}

#[derive(Default)]
pub struct OvlFileBlock {
    pub blocks: Vec<Vec<u8>>,
    pub rel_offset: u32,
    pub total_size: u32,
}

pub struct OvlCompilerInfo {
    pub byte_start: u32,
    pub byte_end: u32,
    pub name: String,
    pub tokens: Vec<OvlCompilerToken>,
}

pub struct OvlData {
    pub version: u32,
    pub subversion: u32,
    pub references: Vec<String>,
    pub loaders: Vec<OvlLoader>,
    pub file_blocks: [OvlFileBlock; 9],
    pub relocations: Vec<u32>,
    pub extra_data: Vec<Vec<u8>>,
    pub compiler_info: Vec<OvlCompilerInfo>,
    pub rel_offset: u32,
}

pub enum OvlCompilerToken {
    Compiler(String),
    Path(String),
}

impl OvlData {
    fn read_header(reader: &mut ByteCursor) -> Result<(u32, u32, Vec<String>), ByteCursorError> {
        let _magic = reader.read::<u32>()?;
        reader.advance(4)?;
        let version = reader.read::<u32>()?;

        let mut reference_count = reader.read::<u32>()?;
        let mut subversion = 0u32;

        if version == 4 {
            reference_count = reader.read::<u32>()?;
        } else if version == 5 {
            subversion = reader.read::<u32>()?;
            if subversion != 0 {
                reader.advance(12)?;
                let mut bytes_read = 0;
                while reader.read::<u8>()? != 0 {
                    bytes_read += 1;
                }
                let padding = (4 - (bytes_read + 1) % 4) % 4;
                reader.advance(padding)?;
            }
            reference_count = reader.read::<u32>()?;
        }

        let mut references = Vec::with_capacity(reference_count as usize);
        for _ in 0..reference_count {
            references.push(reader.read_string_le::<u16>("ascii")?);
        }
        reader.advance(4)?;

        Ok((version, subversion, references))
    }

    fn read_compiler_info_section(
        reader: &mut ByteCursor,
    ) -> Result<OvlCompilerInfo, ByteCursorError> {
        let byte_start = reader.read::<u32>()?;
        let byte_end = reader.read::<u32>()?;
        let _unknown_01 = reader.read::<u32>()?;
        let name = reader.read_string_le::<u16>("ascii")?;
        let _unknown_02 = reader.read::<u32>()?;
        let unknown_03 = reader.read::<u32>()?;
        assert!(unknown_03 == 1);

        let mut tokens = Vec::new();
        loop {
            match reader.read::<u8>()? {
                3 => {
                    let compiler = reader.read_string_le::<u16>("ascii")?;
                    tokens.push(OvlCompilerToken::Compiler(compiler));
                    if reader.peek::<u8>()? == 2 {
                        continue;
                    }
                    reader.advance(1)?;
                    if reader.peek::<u32>()? as i32 == -1 {
                        reader.advance(4)?;
                        break;
                    }
                    reader.advance(8)?;
                }
                2 => {
                    let path = reader.read_string_le::<u16>("ascii")?;
                    tokens.push(OvlCompilerToken::Path(path));
                    if reader.read::<u8>()? == 1 {
                        reader.advance(5)?;
                    }
                    if reader.peek::<u32>()? as i32 == -1 {
                        reader.advance(4)?;
                        break;
                    }
                    reader.advance(8)?;
                }
                other => unimplemented!("indicator = {}", other),
            }
        }

        Ok(OvlCompilerInfo {
            byte_start,
            byte_end,
            name,
            tokens,
        })
    }

    fn read_loaders(reader: &mut ByteCursor, version: u32) -> ByteCursorResult<Vec<OvlLoader>> {
        let loader_count = reader.read::<u32>()?;

        let mut loaders = Vec::with_capacity(loader_count as usize);
        for _ in 0..loader_count {
            let category = reader.read_string_le::<u16>("ascii")?;
            let name = reader.read_string_le::<u16>("ascii")?;
            let variant = reader.read::<u32>()?;
            let tag = reader.read_string_le::<u16>("ascii")?;
            loaders.push(OvlLoader {
                category,
                name,
                variant,
                tag,
                symbols: 0,
            });
        }

        if version == 5 {
            for _ in 0..loader_count {
                let index = reader.read::<u32>()? as usize;
                loaders[index].symbols = reader.read::<u32>()?;
            }
        }

        Ok(loaders)
    }

    fn read_sizes(
        reader: &mut ByteCursor,
        version: u32,
        subversion: u32,
    ) -> ByteCursorResult<[OvlFileBlock; 9]> {
        let mut file_blocks: [OvlFileBlock; 9] = std::array::from_fn(|_| OvlFileBlock::default());

        for i in 0..9 {
            let size = reader.read::<u32>()? as usize;
            file_blocks[i].blocks.resize(size, Vec::new());
            if version > 1 {
                reader.advance(4)?;
                if version == 5 && subversion == 1 {
                    reader.advance(4)?;
                }
                for j in 0..file_blocks[i].blocks.len() as usize {
                    let size = reader.read::<u32>()?;
                    file_blocks[i].blocks[j].resize(size as usize, 0u8);
                    file_blocks[i].total_size += size;
                }
            }
        }

        if version == 4 {
            reader.advance(8)?;
        } else if version == 5 {
            let unknown_byte_count = reader.read::<u32>()? as usize;
            reader.advance(unknown_byte_count)?;
            let unknown_long_count = reader.read::<u32>()? as usize;
            reader.advance(4 * unknown_long_count)?;
        }

        Ok(file_blocks)
    }

    fn read_blocks(
        reader: &mut ByteCursor,
        version: u32,
        file_blocks: &mut [OvlFileBlock; 9],
    ) -> ByteCursorResult<(u32, Vec<u8>)> {
        let mut rel_offset = 0u32;

        let mut loader_table: Vec<u8> = Vec::new();
        for i in 0..9 {
            file_blocks[i].rel_offset = rel_offset;
            for j in 0..file_blocks[i].blocks.len() as usize {
                let size = if version == 1 {
                    let s = reader.read::<u32>()?;
                    file_blocks[i].blocks[j].resize(s as usize, 0u8);
                    file_blocks[i].total_size += s;
                    s as usize
                } else {
                    file_blocks[i].blocks[j].len()
                };

                if i == 2 && j == 1 {
                    loader_table = reader.read_array::<u8>(size)?;
                    file_blocks[i].blocks[j] = loader_table.clone();
                } else {
                    file_blocks[i].blocks[j] = reader.read_array::<u8>(size)?;
                }

                rel_offset += size as u32;
            }
        }

        Ok((rel_offset, loader_table))
    }

    fn read_relocations(
        reader: &mut ByteCursor,
        version: u32,
        subversion: u32,
    ) -> ByteCursorResult<Vec<u32>> {
        let relocation_count = reader.read::<u32>()?;
        let mut relocations = Vec::with_capacity(relocation_count as usize);
        for _ in 0..relocation_count {
            relocations.push(reader.read::<u32>()?);
        }

        if version == 4 || (version == 5 && subversion == 1) {
            reader.advance(4)?;
        }

        Ok(relocations)
    }

    fn read_extra_data(
        reader: &mut ByteCursor,
        version: u32,
        loader_table: Vec<u8>,
    ) -> ByteCursorResult<Vec<Vec<u8>>> {
        let extra_chunk_count: u32 = loader_table
            .chunks_exact(20)
            .map(|rec| {
                let raw = u32::from_le_bytes(rec[8..12].try_into().unwrap());
                if version == 5 { raw & 0xFFFF } else { raw }
            })
            .sum();

        let mut extra_data = Vec::new();

        for _ in 0..extra_chunk_count {
            let size = reader.read::<u32>()? as usize;
            let data = reader.read_array::<u8>(size)?;
            extra_data.push(data);
        }

        Ok(extra_data)
    }

    fn read_compiler_info(reader: &mut ByteCursor) -> ByteCursorResult<Vec<OvlCompilerInfo>> {
        let mut compiler_info = Vec::new();
        while !reader.is_eof() {
            compiler_info.push(Self::read_compiler_info_section(reader)?);
        }
        Ok(compiler_info)
    }

    pub fn read(ovl_id: &str, common: bool) -> ByteCursorResult<Self> {
        let file_path = get_full_path(ovl_id, common);
        let mut reader = ByteCursor::read_file(&file_path, false)?;
        let (version, subversion, references) = Self::read_header(&mut reader)?;
        let loaders = Self::read_loaders(&mut reader, version)?;
        let mut file_blocks = Self::read_sizes(&mut reader, version, subversion)?;
        let (rel_offset, loader_table) = Self::read_blocks(&mut reader, version, &mut file_blocks)?;
        let relocations = Self::read_relocations(&mut reader, version, subversion)?;
        let extra_data = Self::read_extra_data(&mut reader, version, loader_table)?;
        let compiler_info = Self::read_compiler_info(&mut reader)?;
        assert!(reader.is_eof());

        Ok(Self {
            version,
            subversion,
            references,
            loaders,
            file_blocks,
            relocations,
            extra_data,
            compiler_info,
            rel_offset,
        })
    }
}
