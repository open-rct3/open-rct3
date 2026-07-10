use std::collections::HashSet;

use notate::cursors::byte_cursor::ByteCursor;

use crate::{
    error::OvlResult,
    parser::{Ovl, OvlResource},
};

#[derive(Clone, Debug, Default)]
pub struct Sound {
    pub name: String,
    pub tag: u16,
    pub num_channels: u16,
    pub sample_rate: u32,
    pub byte_rate: u32,
    pub block_align: u16,
    pub bits_per_sample: u16,
    pub sound_loop: i32,
    pub channel_1: Vec<u8>,
    pub channel_2: Vec<u8>,
}

impl Sound {
    pub fn read(resource: &OvlResource, ovl: &Ovl) -> OvlResult<Self> {
        let buffer = ovl.data(resource.address, 80).unwrap();
        let mut cursor = ByteCursor::from_bytes(buffer, false);

        let tag = cursor.read::<u16>()?;
        let num_channels = cursor.read::<u16>()?;
        let sample_rate = cursor.read::<u32>()?;
        let byte_rate = cursor.read::<u32>()?;
        let block_align = cursor.read::<u16>()?;
        let bits_per_sample = cursor.read::<u16>()?;

        let metadata = cursor.read_array::<f32>(11)?;
        let metadata_expected = vec![
            0f32, 0f32, 0.05f32, 0.00004f32, 1f32, 0f32, 1500000f32, 0f32, 0.05f32, 2f32, 30f32,
        ];
        assert!(metadata == metadata_expected);

        let sound_loop = cursor.read::<i32>()?;
        let channel_1_index = cursor.read::<u32>()?;
        let channel_1_size = cursor.read::<i32>()?;
        let channel_2_index = cursor.read::<u32>()?;
        let channel_2_size = cursor.read::<i32>()?;

        let name = resource
            .name
            .strip_suffix(":snd")
            .unwrap_or(&resource.name)
            .to_string();

        let channel_1 = ovl
            .data(channel_1_index, channel_1_size as usize)
            .unwrap_or(&[])
            .to_vec();
        let channel_2: Vec<u8> = if num_channels == 2 && channel_2_size > 0 {
            ovl.data(channel_2_index, channel_2_size as usize)
                .unwrap_or(&[])
                .to_vec()
        } else {
            Vec::new()
        };

        Ok(Self {
            name,
            tag,
            num_channels,
            sample_rate,
            byte_rate,
            block_align,
            bits_per_sample,
            sound_loop,
            channel_1,
            channel_2,
        })
    }

    pub fn write(&self, path: &str) -> OvlResult<()> {
        let mut cursor = ByteCursor::empty(false);

        let pcm: Vec<u8> = if self.num_channels == 2 && !self.channel_2.is_empty() {
            let frames = (self.channel_1.len() / 2).min(self.channel_2.len() / 2);
            let mut out = Vec::with_capacity(frames * 4);
            for i in 0..frames {
                out.extend_from_slice(&self.channel_1[i * 2..i * 2 + 2]);
                out.extend_from_slice(&self.channel_2[i * 2..i * 2 + 2]);
            }
            out
        } else {
            self.channel_1.to_vec()
        };

        let pcm_size = pcm.len() as u32;

        cursor.write_string("RIFF", "ascii")?;
        cursor.write::<u32>(36 + pcm_size);
        cursor.write_string("WAVE", "ascii")?;
        cursor.write_string("fmt ", "ascii")?;
        cursor.write::<u32>(16);
        cursor.write::<u16>(self.tag);
        cursor.write::<u16>(self.num_channels);
        cursor.write::<u32>(self.sample_rate);
        cursor.write::<u32>(self.byte_rate);
        cursor.write::<u16>(self.block_align);
        cursor.write::<u16>(self.bits_per_sample);
        cursor.write_string("data", "ascii")?;
        cursor.write::<u32>(pcm_size);
        cursor.write_array::<u8>(&pcm);
        cursor.write_file(path)?;
        Ok(())
    }
}

pub fn parse_snd(
    data: &Ovl,
    resource: &OvlResource,
    compiler_paths: &HashSet<String>,
) -> OvlResult<()> {
    let data = Sound::read(resource, &data)?;
    let test_path = if data.sound_loop == 0 {
        format!("/{}.wav", data.name)
    } else {
        format!("/{}_loop.wav", data.name)
    };
    let matches: Vec<_> = compiler_paths
        .iter()
        .filter(|path| path.to_lowercase().contains(&test_path.to_lowercase()))
        .collect();
    assert!(matches.len() == 1, "{} matched wrong", data.name);
    data.write(matches[0])?;
    Ok(())
}
