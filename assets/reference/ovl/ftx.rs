use image::{ImageBuffer, Rgba};

use crate::{
    decoder::flip_vertical,
    error::{OvlError, OvlResult},
    parser::{Ovl, OvlResource},
    reader::le_u32,
};

pub struct FlexiTexture {
    pub name: String,
    pub width: u32,
    pub height: u32,
    pub recolorable: u32,
    pub rgba: Vec<u8>,
}

impl FlexiTexture {
    pub fn decode(res: &OvlResource, ovl: &Ovl) -> OvlResult<Self> {
        let info = ovl.resource_data(res)?;

        let fts2_addr = le_u32(info, 32);
        let fts = ovl.data(fts2_addr, 28)?;

        let width = le_u32(fts, 4);
        let height = le_u32(fts, 8);
        let recolorable = le_u32(fts, 12);
        let palette_addr = le_u32(fts, 16);
        let texture_addr = le_u32(fts, 20);
        let alpha_addr = le_u32(fts, 24);

        let pixel_count = (width * height) as usize;
        let palette = ovl.data(palette_addr, 1024)?;
        let indices = ovl.data(texture_addr, pixel_count)?;
        let alpha = if alpha_addr != 0 {
            ovl.data(alpha_addr, pixel_count).ok()
        } else {
            None
        };

        let mut rgba = vec![0u8; pixel_count * 4];
        for i in 0..pixel_count {
            let entry = le_u32(palette, indices[i] as usize * 4);
            let o = i * 4;
            rgba[o] = ((entry >> 16) & 0xFF) as u8;
            rgba[o + 1] = ((entry >> 8) & 0xFF) as u8;
            rgba[o + 2] = (entry & 0xFF) as u8;
            rgba[o + 3] = alpha.map(|a| a[i]).unwrap_or(0xFF);
        }

        Ok(Self {
            name: res.name.clone(),
            width,
            height,
            recolorable,
            rgba,
        })
    }

    pub fn export(&self) -> OvlResult<()> {
        let buffer = &mut self.rgba.clone();
        flip_vertical(buffer, self.width, self.height);
        let img: ImageBuffer<Rgba<u8>, _> =
            ImageBuffer::from_raw(self.width, self.height, buffer.to_vec())
                .ok_or_else(|| OvlError::BadResource(self.name.to_string()))?;
        img.save(format!("output/{}.png", self.name))?;
        Ok(())
    }
}
