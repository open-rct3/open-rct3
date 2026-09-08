#![allow(unused)]

use notate::cursors::byte_cursor::{ByteCursor, ByteCursorError, ByteCursorResult};

#[derive(Debug)]
pub struct FieldValueStruct {
    size: usize,
    entries: Vec<StructEntry>,
}

impl FieldValueStruct {
    pub fn entries(&self) -> &[StructEntry] {
        &self.entries
    }

    pub fn by_name<'a>(&'a self, name: &str) -> impl Iterator<Item = &'a StructEntry> {
        self.entries.iter().filter(move |e| e.name == name)
    }

    pub fn first_by_name(&self, name: &str) -> DataResult<&StructEntry> {
        self.entries
            .iter()
            .find(|e| e.name == name)
            .ok_or_else(|| DataError::NoKey(name.to_string()))
    }
}

#[derive(Debug)]
pub struct FieldValueArray {
    size: usize,
    length: usize,
    pub elements: Vec<FieldValueStruct>,
}

#[derive(Debug)]
pub struct FieldValueList {
    size: usize,
    length: usize,
    pub elements: Vec<FieldValueStruct>,
}

#[derive(Debug)]
pub enum FieldValue {
    Bool(bool),
    Int8(i8),
    Int16(i16),
    Int32(i32),
    UInt8(u8),
    UInt16(u16),
    UInt32(u32),
    Float32(f32),
    Vector3([f32; 3]),
    Matrix44([[f32; 4]; 4]),
    Orientation([f32; 3]),
    ManagedObjectPtr(u64),
    Reference(u64),
    String(String),
    Array(FieldValueArray),
    List(FieldValueList),
    Struct(FieldValueStruct),
    GraphedValue { size: usize },
    WaterManager { size: usize },
    GETerrain { size: usize },
    SkirtTrees { size: usize },
    PathTileList { size: usize },
    WaypointList { size: usize },
    FlexiCacheList { size: usize },
    ManagedImage { size: usize },
    PathNodeArray { size: usize },
    ResourceSymbol { size: usize },
    StringTable { size: usize },
    BlockingScenery { size: usize },
}

impl FieldValue {
    pub fn kind(&self) -> FieldKind {
        match self {
            FieldValue::Bool(_) => FieldKind::Bool,
            FieldValue::Int8(_) => FieldKind::Int8,
            FieldValue::Int16(_) => FieldKind::Int16,
            FieldValue::Int32(_) => FieldKind::Int32,
            FieldValue::UInt8(_) => FieldKind::UInt8,
            FieldValue::UInt16(_) => FieldKind::UInt16,
            FieldValue::UInt32(_) => FieldKind::UInt32,
            FieldValue::Float32(_) => FieldKind::Float32,
            FieldValue::Vector3(_) => FieldKind::Vector3,
            FieldValue::Matrix44(_) => FieldKind::Matrix44,
            FieldValue::Orientation(_) => FieldKind::Orientation,
            FieldValue::ManagedObjectPtr(_) => FieldKind::ManagedObjectPtr,
            FieldValue::Reference(_) => FieldKind::Reference,
            FieldValue::String(_) => FieldKind::String,
            FieldValue::Array(_) => FieldKind::Array,
            FieldValue::List(_) => FieldKind::List,
            FieldValue::Struct(_) => FieldKind::Struct,
            FieldValue::GraphedValue { .. } => FieldKind::GraphedValue,
            FieldValue::WaterManager { .. } => FieldKind::WaterManager,
            FieldValue::GETerrain { .. } => FieldKind::GETerrain,
            FieldValue::SkirtTrees { .. } => FieldKind::SkirtTrees,
            FieldValue::PathTileList { .. } => FieldKind::PathTileList,
            FieldValue::WaypointList { .. } => FieldKind::WaypointList,
            FieldValue::FlexiCacheList { .. } => FieldKind::FlexiCacheList,
            FieldValue::ManagedImage { .. } => FieldKind::ManagedImage,
            FieldValue::PathNodeArray { .. } => FieldKind::PathNodeArray,
            FieldValue::ResourceSymbol { .. } => FieldKind::ResourceSymbol,
            FieldValue::StringTable { .. } => FieldKind::StringTable,
            FieldValue::BlockingScenery { .. } => FieldKind::BlockingScenery,
        }
    }
}

#[derive(Debug)]
pub enum FieldKind {
    Bool,
    Int8,
    Int16,
    Int32,
    UInt8,
    UInt16,
    UInt32,
    Float32,
    Vector3,
    Matrix44,
    Orientation,
    ManagedObjectPtr,
    Reference,
    String,
    Array,
    List,
    Struct,
    GraphedValue,
    WaterManager,
    GETerrain,
    SkirtTrees,
    PathTileList,
    WaypointList,
    FlexiCacheList,
    ManagedImage,
    PathNodeArray,
    ResourceSymbol,
    StringTable,
    BlockingScenery,
}

impl From<String> for FieldKind {
    fn from(value: String) -> Self {
        match value.as_str() {
            "array" => Self::Array,
            "list" => Self::List,
            "bool" => Self::Bool,
            "float32" => Self::Float32,
            "int8" => Self::Int8,
            "int16" => Self::Int16,
            "int32" => Self::Int32,
            "managedobjectptr" => Self::ManagedObjectPtr,
            "matrix44" => Self::Matrix44,
            "orientation" => Self::Orientation,
            "reference" => Self::Reference,
            "uint8" => Self::UInt8,
            "uint16" => Self::UInt16,
            "uint32" => Self::UInt32,
            "vector3" => Self::Vector3,
            "struct" => Self::Struct,
            "string" => Self::String,
            "graphedValue" => Self::GraphedValue,
            "WaterManager" => Self::WaterManager,
            "GE_Terrain" => Self::GETerrain,
            "SkirtTrees" => Self::SkirtTrees,
            "PathTileList" => Self::PathTileList,
            "waypointlist" => Self::WaypointList,
            "flexicachelist" => Self::FlexiCacheList,
            "managedImage" => Self::ManagedImage,
            "pathnodearray" => Self::PathNodeArray,
            "resourcesymbol" => Self::ResourceSymbol,
            "stringTable" => Self::StringTable,
            "BlockingScenery" => Self::BlockingScenery,
            other => unimplemented!("Field kind = {}", other),
        }
    }
}

#[derive(Debug)]
pub struct StructEntry {
    pub name: String,
    pub value: FieldValue,
}

impl StructEntry {
    pub fn as_bool(&self) -> DataResult<bool> {
        match &self.value {
            FieldValue::Bool(value) => Ok(*value),
            _ => Err(DataError::WrongType {
                name: self.name.clone(),
                expected: FieldKind::Bool,
            }),
        }
    }

    pub fn as_int32(&self) -> DataResult<i32> {
        match &self.value {
            FieldValue::Int32(value) => Ok(*value),
            _ => Err(DataError::WrongType {
                name: self.name.clone(),
                expected: FieldKind::Int32,
            }),
        }
    }

    pub fn as_struct(&self) -> DataResult<&FieldValueStruct> {
        match &self.value {
            FieldValue::Struct(value) => Ok(value),
            _ => Err(DataError::WrongType {
                name: self.name.clone(),
                expected: FieldKind::Struct,
            }),
        }
    }

    pub fn as_array(&self) -> DataResult<&FieldValueArray> {
        match &self.value {
            FieldValue::Array(value) => Ok(value),
            _ => Err(DataError::WrongType {
                name: self.name.clone(),
                expected: FieldKind::Array,
            }),
        }
    }

    pub fn as_string(&self) -> DataResult<String> {
        match &self.value {
            FieldValue::String(value) => Ok(value.clone()),
            _ => Err(DataError::WrongType {
                name: self.name.clone(),
                expected: FieldKind::String,
            }),
        }
    }

    pub fn as_ptr(&self) -> DataResult<u64> {
        match &self.value {
            FieldValue::ManagedObjectPtr(value) => Ok(*value),
            _ => Err(DataError::WrongType {
                name: self.name.clone(),
                expected: FieldKind::ManagedObjectPtr,
            }),
        }
    }

    pub fn as_ref(&self) -> DataResult<u64> {
        match &self.value {
            FieldValue::Reference(value) => Ok(*value),
            _ => Err(DataError::WrongType {
                name: self.name.clone(),
                expected: FieldKind::Reference,
            }),
        }
    }

    pub fn kind(&self) -> FieldKind {
        self.value.kind()
    }
}

#[derive(Debug)]
pub struct StructField {
    name: String,
    kind: FieldKind,
    size: usize,
    fields: Vec<StructField>,
}

impl StructField {
    pub fn read_definition(reader: &mut ByteCursor) -> ByteCursorResult<Self> {
        let name = reader.read_string_le::<u16>("ascii")?;
        let kind_str = reader.read_string_le::<u16>("ascii")?;
        let kind = FieldKind::from(kind_str);
        let size = reader.read::<u32>()? as usize;
        let field_count = reader.read::<u32>()?;
        let mut fields = Vec::new();
        for _ in 0..field_count {
            let field = StructField::read_definition(reader)?;
            fields.push(field);
        }
        Ok(Self {
            name,
            kind,
            size,
            fields,
        })
    }

    pub fn read_entry(&self, reader: &mut ByteCursor) -> ByteCursorResult<StructEntry> {
        let value = match self.kind {
            FieldKind::Bool => FieldValue::Bool(reader.read::<bool>()?),
            FieldKind::Int8 => FieldValue::Int8(reader.read::<i8>()?),
            FieldKind::Int16 => FieldValue::Int16(reader.read::<i16>()?),
            FieldKind::Int32 => FieldValue::Int32(reader.read::<i32>()?),
            FieldKind::UInt8 => FieldValue::UInt8(reader.read::<u8>()?),
            FieldKind::UInt16 => FieldValue::UInt16(reader.read::<u16>()?),
            FieldKind::UInt32 => FieldValue::UInt32(reader.read::<u32>()?),
            FieldKind::Float32 => FieldValue::Float32(reader.read::<f32>()?),
            FieldKind::Vector3 => {
                let mut value = [0.0; 3];
                for i in 0..3 {
                    value[i] = reader.read::<f32>()?;
                }
                FieldValue::Vector3(value)
            }
            FieldKind::Matrix44 => {
                let mut value = [[0.0; 4]; 4];
                for row in 0..4 {
                    for col in 0..4 {
                        value[row][col] = reader.read::<f32>()?;
                    }
                }
                FieldValue::Matrix44(value)
            }
            FieldKind::Orientation => {
                let mut value = [0.0; 3];
                for i in 0..3 {
                    value[i] = reader.read::<f32>()?;
                }
                FieldValue::Orientation(value)
            }
            FieldKind::ManagedObjectPtr => FieldValue::ManagedObjectPtr(reader.read::<u64>()?),
            FieldKind::Reference => FieldValue::Reference(reader.read::<u64>()?),
            FieldKind::String => {
                let mut length = reader.read::<u32>()? as usize;
                let mut encoding = "ascii";
                if reader.peek::<u32>()? == 0xEFEFEFEF {
                    reader.advance(4)?;
                    length -= 4;
                    encoding = "utf16";
                }
                let value = reader.read_string(length, encoding)?;
                FieldValue::String(value)
            }
            FieldKind::Array => {
                let size = reader.read::<u32>()? as usize;
                let length = reader.read::<u32>()? as usize;
                let mut elements = Vec::with_capacity(length);
                for _ in 0..length {
                    let mut item = Vec::with_capacity(self.fields.len());
                    for field in &self.fields {
                        item.push(field.read_entry(reader)?);
                    }
                    elements.push(FieldValueStruct {
                        size: 0,
                        entries: item,
                    });
                }
                FieldValue::Array(FieldValueArray {
                    size,
                    length,
                    elements,
                })
            }
            FieldKind::List => {
                let size = reader.read::<u32>()? as usize;
                let length = reader.read::<u32>()? as usize;
                let mut elements = Vec::with_capacity(length);
                for _ in 0..length {
                    let mut item = Vec::with_capacity(self.fields.len());
                    for field in &self.fields {
                        item.push(field.read_entry(reader)?);
                    }
                    elements.push(FieldValueStruct {
                        size: 0,
                        entries: item,
                    });
                }
                FieldValue::List(FieldValueList {
                    size,
                    length,
                    elements,
                })
            }
            FieldKind::Struct => {
                let size = if self.size == 0 {
                    reader.read::<u32>()? as usize
                } else {
                    self.size
                };
                let mut entries = Vec::new();
                for field in &self.fields {
                    let value = field.read_entry(reader)?;
                    entries.push(value);
                }
                FieldValue::Struct(FieldValueStruct { size, entries })
            }
            FieldKind::GraphedValue => {
                let size = if self.size == 0 {
                    reader.read::<u32>()? as usize
                } else {
                    self.size
                };
                reader.advance(size)?;
                FieldValue::GraphedValue { size }
            }
            FieldKind::WaterManager => {
                let size = if self.size == 0 {
                    reader.read::<u32>()? as usize
                } else {
                    self.size
                };
                reader.advance(size)?;
                FieldValue::WaterManager { size }
            }
            FieldKind::GETerrain => {
                let size = if self.size == 0 {
                    reader.read::<u32>()? as usize
                } else {
                    self.size
                };
                reader.advance(size)?;
                FieldValue::GETerrain { size }
            }
            FieldKind::SkirtTrees => {
                let size = if self.size == 0 {
                    reader.read::<u32>()? as usize
                } else {
                    self.size
                };
                reader.advance(size)?;
                FieldValue::SkirtTrees { size }
            }
            FieldKind::PathTileList => {
                let size = if self.size == 0 {
                    reader.read::<u32>()? as usize
                } else {
                    self.size
                };
                reader.advance(size)?;
                FieldValue::PathTileList { size }
            }
            FieldKind::WaypointList => {
                let size = if self.size == 0 {
                    reader.read::<u32>()? as usize
                } else {
                    self.size
                };
                reader.advance(size)?;
                FieldValue::WaypointList { size }
            }
            FieldKind::FlexiCacheList => {
                let size = if self.size == 0 {
                    reader.read::<u32>()? as usize
                } else {
                    self.size
                };
                reader.advance(size)?;
                FieldValue::FlexiCacheList { size }
            }
            FieldKind::ManagedImage => {
                let size = if self.size == 0 {
                    reader.read::<u32>()? as usize
                } else {
                    self.size
                };
                reader.advance(size)?;
                FieldValue::ManagedImage { size }
            }
            FieldKind::PathNodeArray => {
                let size = if self.size == 0 {
                    reader.read::<u32>()? as usize
                } else {
                    self.size
                };
                reader.advance(size)?;
                FieldValue::PathNodeArray { size }
            }
            FieldKind::ResourceSymbol => {
                let size = if self.size == 0 {
                    reader.read::<u32>()? as usize
                } else {
                    self.size
                };
                reader.advance(size)?;
                FieldValue::ResourceSymbol { size }
            }
            FieldKind::StringTable => {
                let size = if self.size == 0 {
                    reader.read::<u32>()? as usize
                } else {
                    self.size
                };
                reader.advance(size)?;
                FieldValue::StringTable { size }
            }
            FieldKind::BlockingScenery => {
                let size = if self.size == 0 {
                    reader.read::<u32>()? as usize
                } else {
                    self.size
                };
                reader.advance(size)?;
                FieldValue::BlockingScenery { size }
            }
        };
        Ok(StructEntry {
            name: self.name.clone(),
            value,
        })
    }
}

#[derive(Debug)]
pub struct DataEntry {
    pub id: u64,
    pub name: String,
    pub values: Vec<StructEntry>,
}

impl DataEntry {
    pub fn by_name<'a>(&'a self, name: &str) -> impl Iterator<Item = &'a StructEntry> {
        self.values.iter().filter(move |e| e.name == name)
    }

    pub fn first_by_name(&self, name: &str) -> DataResult<&StructEntry> {
        self.values
            .iter()
            .find(|e| e.name == name)
            .ok_or_else(|| DataError::NoKey(name.to_string()))
    }
}

#[derive(Debug)]
pub struct DataStruct {
    name: String,
    fields: Vec<StructField>,
}

impl DataStruct {
    pub fn read_definition(reader: &mut ByteCursor) -> ByteCursorResult<Self> {
        let name = reader.read_string_le::<u16>("ascii")?;
        let field_count = reader.read::<u32>()?;
        let mut fields = Vec::new();
        for _ in 0..field_count {
            let field = StructField::read_definition(reader)?;
            fields.push(field);
        }
        Ok(Self { name, fields })
    }

    pub fn read_entry(&self, reader: &mut ByteCursor, id: u64) -> ByteCursorResult<DataEntry> {
        let mut values = Vec::new();
        for field in &self.fields {
            let value = field.read_entry(reader)?;
            values.push(value);
        }
        Ok(DataEntry {
            id,
            name: self.name.clone(),
            values,
        })
    }
}

#[derive(Debug)]
pub struct DataFile {
    pub entries: Vec<DataEntry>,
}

impl DataFile {
    pub fn read(file_path: &str) -> ByteCursorResult<Self> {
        let mut reader = ByteCursor::read_file(file_path, false)?;

        let extended_header = reader.peek::<u32>()? == 0;

        if extended_header {
            reader.advance(8)?;
            let version = reader.read::<u8>()?;
            match version {
                0x1A => reader.set_offset(0x40),
                0x2A => reader.set_offset(0x50),
                other => unimplemented!("Version = {}", other),
            }
        }

        let struct_count = reader.read::<u32>()?;
        let mut structs = Vec::new();
        for _ in 0..struct_count {
            let entry = DataStruct::read_definition(&mut reader)?;
            structs.push(entry);
        }

        let entry_count = reader.read::<u32>()?;
        let mut entries = Vec::with_capacity(entry_count as usize);
        for _ in 0..entry_count {
            let struct_index = reader.read::<u32>()? as usize;
            let id = reader.read::<u64>()?;
            let entry = &structs[struct_index];
            let value = entry.read_entry(&mut reader, id)?;
            entries.push(value);
        }

        Ok(Self { entries })
    }

    pub fn by_name<'a>(&'a self, name: &str) -> impl Iterator<Item = &'a DataEntry> {
        self.entries.iter().filter(move |e| e.name == name)
    }

    pub fn first_by_name(&self, name: &str) -> DataResult<&DataEntry> {
        self.entries
            .iter()
            .find(|e| e.name == name)
            .ok_or_else(|| DataError::NoKey(name.to_string()))
    }

    pub fn by_id(&self, id: u64) -> DataResult<&DataEntry> {
        self.entries
            .iter()
            .find(|e| e.id == id)
            .ok_or_else(|| DataError::NoKey(id.to_string()))
    }
}

#[derive(Debug)]
pub enum DataError {
    Reader(ByteCursorError),
    NoKey(String),
    WrongType { name: String, expected: FieldKind },
}

impl From<ByteCursorError> for DataError {
    fn from(value: ByteCursorError) -> Self {
        DataError::Reader(value)
    }
}

pub type DataResult<T> = Result<T, DataError>;
