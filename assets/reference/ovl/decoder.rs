use crate::{
    error::OvlResult, formats::{
        ftx::FlexiTexture,
        tex::{Texture, TextureContext},
    }, parser::Ovl,
};

pub fn flip_vertical(pixels: &mut [u8], width: u32, height: u32) {
    let row = (width as usize) * 4;
    let h = height as usize;
    if row == 0 || h < 2 || pixels.len() < row * h {
        return;
    }
    for y in 0..h / 2 {
        let top = y * row;
        let bot = (h - 1 - y) * row;
        let (head, tail) = pixels.split_at_mut(bot);
        head[top..top + row].swap_with_slice(&mut tail[..row]);
    }
}

pub fn decode_ovl(file_id: &str) -> OvlResult<()> {
    let ovl = Ovl::read(file_id)?;
    let textures = TextureContext::build(&ovl)?;

    for resource in ovl.resources() {
        match resource.tag.as_str() {
            "anr" => {}
            "asd" => {}
            "ban" => {}
            "bsh" => {}
            "ced" => {}
            "chg" => {}
            "cid" => {}
            "enc" => {}
            "ent" => {}
            "fct" => {}
            "flt" => {}
            "ftx" => {
                let data = FlexiTexture::decode(resource, &ovl)?;
                data.export()?;
            }
            "gsi" => {}
            "int" => {}
            "mam" => {}
            "mdl" => {}
            "mms" => {}
            "modelanim" => {}
            "phd" => {}
            "ppg" => {}
            "prt" => {}
            "psi" => {}
            "ptd" => {}
            "pty" => {}
            "qtd" => {}
            "rcg" => {}
            "ric" => {}
            "rit" => {}
            "sal" => {}
            "san" => {}
            "sat" => {}
            "shs" => {}
            "sid" => {}
            "snd" => {}
            "spl" => {}
            "ssk" => {}
            "sta" => {}
            "svd" => {}
            "ter" => {}
            "tex" => {
                if let Some(data) = Texture::decode(resource, &ovl, &textures)? {
                    data.export()?;
                } else {
                    println!("No data: {:?}", resource);
                }
            }
            "tks" => {}
            "trr" => {}
            "txt" => {}
            "vwg" => {}
            "wad" => {}
            "wai" => {}
            "was" => {}
            other => todo!("{}", other),
        }
    }

    Ok(())
}
