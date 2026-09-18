use crate::{
    error::{OvlError, OvlResult},
    parser::{Ovl, OvlKind},
    reader::{OvlLoaderEntry, le_u32},
};

pub struct BitmapEntry {
    pub format: u32,
    pub width: u32,
    pub height: u32,
    pub mipcount: u32,
    pub mips: Vec<Vec<u8>>,
}

pub struct BitmapTable {
    pub entries: Vec<BitmapEntry>,
}

impl BitmapTable {
    pub fn decode_entry(ovl: &Ovl, kind: OvlKind, entry: &OvlLoaderEntry) -> OvlResult<Self> {
        let count = ovl.read_u32(entry.data_address + 4)? as usize;
        let chunks = ovl.extra_chunks_for(kind, entry);

        let [header_chunk, pixels] = chunks else {
            return Err(OvlError::BadResource(format!(
                "btbl at {:#x}: expected 2 extra chunks, got {}",
                entry.data_address,
                chunks.len()
            )));
        };
        let headers = &header_chunk[8..];

        let mut entries = Vec::with_capacity(count);
        let mut cursor = 0usize;
        for i in 0..count {
            let h = &headers[i * 16..];
            let (format, width, height, mipcount) =
                (le_u32(h, 0), le_u32(h, 4), le_u32(h, 8), le_u32(h, 12));

            let (mut bw, mut bh, bpp) = match format {
                0x02 => (width, height, 4),
                0x12 => (width / 4, height / 4, 8),
                0x13 | 0x14 => (width / 4, height / 4, 16),
                other => return Err(OvlError::UnknownFormat(other)),
            };
            bw = bw.max(1);
            bh = bh.max(1);

            let mut mips = Vec::with_capacity(mipcount as usize);
            for level in 0..mipcount {
                let size = ((bw >> level).max(1) * (bh >> level).max(1) * bpp) as usize;
                mips.push(pixels[cursor..cursor + size].to_vec());
                cursor += size;
            }

            entries.push(BitmapEntry {
                format,
                width,
                height,
                mipcount,
                mips,
            });
        }

        assert_eq!(cursor, pixels.len());
        Ok(Self { entries })
    }
}
