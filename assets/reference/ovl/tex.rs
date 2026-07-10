use std::collections::HashMap;

use image::{ImageBuffer, Rgba};

use crate::decoder::flip_vertical;
use crate::error::{OvlError, OvlResult};
use crate::formats::btbl::{BitmapEntry, BitmapTable};
use crate::parser::{Ovl, OvlKind, OvlResource};
use crate::reader::le_u32;

pub struct TextureContext {
    bitmaps: Vec<BitmapTable>,
    flics: HashMap<u32, (usize, usize)>,
}

impl TextureContext {
    pub fn build(ovl: &Ovl) -> OvlResult<Self> {
        let mut bitmaps = Vec::new();
        let mut flics = HashMap::new();

        for kind in [OvlKind::Common, OvlKind::Unique] {
            let data = ovl.side(kind);
            let mut current_btbl: Option<usize> = None;

            for e in &data.entries {
                let Some(loader) = data.loader_for(e) else {
                    continue;
                };
                match loader.tag.as_str() {
                    "btbl" => {
                        bitmaps.push(BitmapTable::decode_entry(ovl, kind, e)?);
                        current_btbl = Some(bitmaps.len() - 1);
                    }
                    "flic" => {
                        let Some(btbl) = current_btbl else {
                            continue;
                        };
                        let Some(chunk) = ovl.extra_chunks_for(kind, e).first() else {
                            continue;
                        };
                        flics.insert(e.data_address, (btbl, le_u32(chunk, 0) as usize));
                    }
                    _ => {}
                }
            }
        }

        Ok(Self { bitmaps, flics })
    }

    pub fn lookup(&self, flic_addr: u32) -> Option<&BitmapEntry> {
        let &(btbl, index) = self.flics.get(&flic_addr)?;
        self.bitmaps.get(btbl)?.entries.get(index)
    }
}

pub struct Texture {
    pub name: String,
    pub width: u32,
    pub height: u32,
    pub format: u32,
    pub rgba: Vec<u8>,
}

impl Texture {
    pub fn decode(res: &OvlResource, ovl: &Ovl, ctx: &TextureContext) -> OvlResult<Option<Self>> {
        let Some(flic_slot) = ovl.reloc_target(res.address + 52) else {
    if let Some(sref) = ovl.symbol_ref_at(res.address + 52) {
        println!("{}: flic is external symbol {}", res.name, sref.symbol);
    } else if ovl.read_u32(res.address + 52)? != 0 {
        return Err(OvlError::BadResource(format!(
            "{}: non-zero flic slot without relocation",
            res.name
        )));
    }
    return Ok(None);
};
        let Some(flic_addr) = ovl.reloc_target(flic_slot) else {
            return Ok(None);
        };

        // println!(
        //     "{}: flic_slot {flic_slot:#x} flic_addr {flic_addr:#x}",
        //     res.name
        // );

        let bitmap = ctx.lookup(flic_addr).ok_or_else(|| {
            OvlError::BadResource(format!("{}: no flic entry at {flic_addr:#x}", res.name))
        })?;

        let rgba = bitmap_to_rgba(bitmap, 0)?;

        Ok(Some(Self {
            name: res.name.clone(),
            width: bitmap.width,
            height: bitmap.height,
            format: bitmap.format,
            rgba,
        }))
    }

    pub fn export(&self) -> OvlResult<()> {
        let buffer = &mut self.rgba.clone();
        flip_vertical(buffer, self.width, self.height);
        let img: ImageBuffer<Rgba<u8>, _> =
            ImageBuffer::from_raw(self.width, self.height, buffer.to_vec())
                .ok_or_else(|| OvlError::BadResource(self.name.clone()))?;
        img.save(format!("output/{}.png", self.name))?;
        Ok(())
    }
}

pub fn bitmap_to_rgba(bitmap: &BitmapEntry, level: usize) -> OvlResult<Vec<u8>> {
    let data = bitmap
        .mips
        .get(level)
        .ok_or_else(|| OvlError::BadResource(format!("missing mip level {level}")))?;
    let w = (bitmap.width >> level).max(1) as usize;
    let h = (bitmap.height >> level).max(1) as usize;

    match bitmap.format {
        0x02 => {
            let mut rgba = vec![0u8; w * h * 4];
            for i in 0..w * h {
                let entry = le_u32(data, i * 4);
                let o = i * 4;
                rgba[o] = ((entry >> 16) & 0xFF) as u8;
                rgba[o + 1] = ((entry >> 8) & 0xFF) as u8;
                rgba[o + 2] = (entry & 0xFF) as u8;
                rgba[o + 3] = ((entry >> 24) & 0xFF) as u8;
            }
            Ok(rgba)
        }
        0x12 | 0x13 | 0x14 => {
            let format = match bitmap.format {
                0x12 => texpresso::Format::Bc1,
                0x13 => texpresso::Format::Bc2,
                _ => texpresso::Format::Bc3,
            };
            let mut rgba = vec![0u8; w * h * 4];
            format.decompress(data, w, h, &mut rgba);
            Ok(rgba)
        }
        other => Err(OvlError::BadResource(format!(
            "unhandled format {other:#x}"
        ))),
    }
}
