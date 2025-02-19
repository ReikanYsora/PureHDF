# PureHDF

[![GitHub Actions](https://github.com/Apollo3zehn/PureHDF/actions/workflows/build-and-publish.yml/badge.svg)](https://github.com/Apollo3zehn/PureHDF/actions) [![NuGet](https://img.shields.io/nuget/vpre/PureHDF.svg?label=Nuget)](https://www.nuget.org/packages/PureHDF)

A pure C# library without native dependencies that makes reading of HDF5 files (groups, datasets, attributes, links, ...) very easy.

The minimum supported target framework is .NET Standard 2.0 which includes
- .NET Framework 4.6.1+ 
- .NET Core (all versions)
- .NET 5+

This library runs on all platforms (ARM, x86, x64) and operating systems (Linux, Windows, MacOS, Raspbian, etc) that are supported by the .NET ecosystem without special configuration.

The implemention follows the [HDF5 File Format Specification (HDF5 1.10)](https://docs.hdfgroup.org/hdf5/v1_10/_f_m_t3.html).

> Overwhelmed by the number of different HDF 5 libraries? [Here](#13-comparison-table) is a comparison table.

# Content
1. [Objects](#1-objects)
2. [Attributes](#2-attributes)
3. [Data](#3-data)
4. [Data Selection / Data Slicing](#4-data-selection--data-slicing)
5. [Filters](#5-filters)
6. [Reading Compound Data](#6-reading-compound-data)
7. [Reading Multidimensional Data](#7-reading-multidimensional-data)
8. [Concurrency](#8-concurrency)
9. [Amazon S3](#9-amazon-s3)
10. [Highly Scalable Data Service (HSDS)](#10-highly-scalable-data-service-HSDS)
11. [Intellisense](#11-intellisense)
12. [Unsupported Features](#12-unsupported-features)
13. [Comparison Table](#13-comparison-table)

# 1 Objects

```cs
// open HDF5 file, the returned H5File instance represents the root group ('/')
using var root = H5File.OpenRead(filePath);
```

## 1.1 Get Object
### Group

```cs
// get nested group
var group = root.Group("/my/nested/group");
```

### Dataset

```cs

// get dataset in group
var dataset = group.Dataset("myDataset");

// alternatively, use the full path
var dataset = group.Dataset("/my/nested/group/myDataset");
```

### Commited Data Type

```cs
// get commited data type in group
var commitedDatatype = group.CommitedDatatype("myCommitedDatatype");
```

### Any Object Type
When you do not know what kind of link to expect at a given path, use the following code:

```cs
// get H5Object (base class of all HDF5 object types)
var myH5Object = group.Get("/path/to/unknown/object");
```

## 1.2 Additional Info

### Iteration

Iterate through all links in a group:

```cs
foreach (var link in group.Children())
{
    var message = link switch
    {
        IH5Group group               => $"I am a group and my name is '{group.Name}'.",
        IH5Dataset dataset           => $"I am a dataset, call me '{dataset.Name}'.",
        IH5CommitedDatatype datatype => $"I am the data type '{datatype.Name}'.",
        IH5UnresolvedLink lostLink   => $"I cannot find my link target =( shame on '{lostLink.Name}'."
        _                           => throw new Exception("Unknown link type")
    };

    Console.WriteLine(message)
}
```

An `IH5UnresolvedLink` becomes part of the `Children` collection when a symbolic link is dangling, i.e. the link target does not exist or cannot be accessed.

### External Files

There are multiple mechanisms in HDF5 that allow one file to reference another file. The external file resolution algorithm is specific to each of these mechanisms:

| Type | Documentation | Algorithm | Environment variable |
|---|---|---|---|
| External Link | [Link](https://docs.hdfgroup.org/hdf5/v1_10/group___h5_l.html#title5) | [Link](https://github.com/HDFGroup/hdf5/blob/hdf5_1_10_9/src/H5Lpublic.h#L1503-L1566) | `HDF5_EXT_PREFIX` |
| External Dataset Storage | [Link](https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html#title11) | [Link](https://github.com/HDFGroup/hdf5/blob/hdf5_1_10_9/src/H5Ppublic.h#L7084-L7116) | `HDF5_EXTFILE_PREFIX` |
| Virtual Datasets | [Link](https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html#title12) | [Link](https://github.com/HDFGroup/hdf5/blob/hdf5_1_10_9/src/H5Ppublic.h#L6607-L6670) | `HDF5_VDS_PREFIX` |

Usage:

**External Link**

```cs
using PureHDF.VOL.Native;

var group = (INativeGroup)root.Group(...);

var linkAccess = new H5LinkAccess(
    ExternalLinkPrefix: prefix 
);

var dataset = group.Dataset(path, linkAccess);
```

**External Dataset Storage**

```cs
using PureHDF.VOL.Native;

var dataset = (INativeDataset)root.Dataset(...);

var datasetAccess = new H5DatasetAccess(
    ExternalFilePrefix: prefix 
);

var data = dataset.Read<float>(..., datasetAccess: datasetAccess);
```

**Virtual Datasets**

```cs
using PureHDF.VOL.Native;

var dataset = (INativeDataset)root.Dataset(...);

var datasetAccess = new H5DatasetAccess(
    VirtualPrefix: prefix 
);

var data = dataset.Read<float>(..., datasetAccess: datasetAccess);
```

# 2. Attributes

```cs
// get attribute of group
var attribute = group.Attribute("myAttributeOnAGroup");

// get attribute of dataset
var attribute = dataset.Attribute("myAttributeOnADataset");
```

# 3. Data

The following code samples work for datasets as well as attributes.

```cs
// class: fixed-point

    var data = dataset.Read<int>();

// class: floating-point

    var data = dataset.Read<double>();

// class: string

    var data = dataset.ReadString();

// class: bitfield

    [Flags]
    enum SystemStatus : ushort /* make sure the enum in HDF file is based on the same type */
    {
        MainValve_Open          = 0x0001
        AuxValve_1_Open         = 0x0002
        AuxValve_2_Open         = 0x0004
        MainEngine_Ready        = 0x0008
        FallbackEngine_Ready    = 0x0010
        // ...
    }

    var data = dataset.Read<SystemStatus>();
    var readyToLaunch = data[0].HasFlag(SystemStatus.MainValve_Open | SystemStatus.MainEngine_Ready);

// class: opaque

    var data = dataset.Read<byte>();
    var data = dataset.Read<MyOpaqueStruct>();

// class: compound

    /* option 1 (faster) */
    var data = dataset.Read<MyNonNullableStruct>();
    /* option 2 (slower, for more info see the link below after this code block) */
    var data = dataset.ReadCompound<MyNullableStruct>();

// class: reference

    var data = dataset.Read<H5ObjectReference>();
    var firstRef = data.First();

    /* NOTE: Dereferencing would be quite fast if the object's name
     * was known. Instead, the library searches recursively for the  
     * object. Do not dereference using a parent (group) that contains
     * any circular soft links. Hard links are no problem.
     */

    /* option 1 (faster) */
    var firstObject = directParent.Get(firstRef);

    /* option 1 (slower, use if you don't know the objects parent) */
    var firstObject = root.Get(firstRef);

// class: enumerated

    enum MyEnum : short /* make sure the enum in HDF file is based on the same type */
    {
        MyValue1 = 1,
        MyValue2 = 2,
        // ...
    }

    var data = dataset.Read<MyEnum>();

// class: variable length

    var data = dataset.ReadString();

// class: array

    var data = dataset
        .Read<int>()
        /* dataset dims = int[2, 3] */
        /*   array dims = int[4, 5] */
        .ToArray4D(2, 3, 4, 5);

// class: time
// -> not supported (reason: the HDF5 C lib itself does not fully support H5T_TIME)
```

For more information on compound data, see section [Reading compound data](#6-reading-compound-data).

# 4. Data Selection / Data Slicing

## 4.1 Overview

Data selection is one of the strengths of HDF5 and is applicable to all dataset types (contiguous, compact and chunked). With PureHDF, the full dataset can be read with a simple call to `dataset.Read()`. However, if you want to read only parts of the dataset, [selections](https://support.hdfgroup.org/HDF5/Tutor/selectsimple.html) are your friend. 

PureHDF supports three types of selections. These are:

| Type                 | Description                                                                                                                                                                      |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `HyperslabSelection` | A hyperslab is a selection of elements from a hyper rectangle.<br />[HDF5 User's Guide](https://portal.hdfgroup.org/display/HDF5/HDF5+User+Guides) > 7.4.1.1 Hyperslab Selection |
| `PointSelection`     | Selects a collection of points.<br />[HDF5 User's Guide](https://portal.hdfgroup.org/display/HDF5/HDF5+User+Guides) > 7.4.1.2 Select Points                                      |
| `DelegateSelection`  | This selection accepts a custom walker which selects the user defined points or blocks.                                                                                          |

## 4.2 Examples

Selections can be passed to the read method to avoid reading the full dataset like this:

```cs
var fileSelection = ...;
var data = dataset.Read<int>(fileSelection: fileSelection);
```

Alternatively, if the selection should not be applied to the file but to the memory buffer, use the `memorySelection` parameter:

```cs
var memorySelection = ...;
var data = dataset.Read<int>(memorySelection: memorySelection);
```

All parameters are optional. For example, when the `fileSelection` parameter is ommited, the whole dataset will be read. Note that the number of data points in the file selection must always match that of the memory selection.

> Note: There are overload methods that allow you to provide your own buffer.

**Point selection**

Point selections require a two-dimensional `n` x `m` array where `n` is the number of points and `m` the rank of the dataset. Here is an example with four points to select data from a dataset of rank = `3`.

```cs
var selection = new PointSelection(new ulong[,] {
    { 00, 00, 00 },
    { 00, 05, 10 },
    { 12, 01, 10 },
    { 05, 07, 09 }
});
```

**Hyperslab selection**

A hyperslab selection can be used to select a contiguous block of elements or to select multiple blocks.

The simplest example is a selection for a 1-dimensional dataset at a certain offset (`start: 10`) and a certain length (`block: 50`):

```cs
var fileSelection = new HyperslabSelection(start: 10, block: 50);
```

The following - more advanced - example shows selecions for a three-dimensional dataset (source) and a two-dimensional memory buffer (target):

```cs
var dataset = root.Dataset("myDataset");
var memoryDims = new ulong[] { 75, 25 };

var datasetSelection = new HyperslabSelection(
    rank: 3,
    starts: new ulong[] { 2, 2, 0 },
    strides: new ulong[] { 5, 8, 2 },
    counts: new ulong[] { 5, 3, 2 },
    blocks: new ulong[] { 3, 5, 2 }
);

var memorySelection = new HyperslabSelection(
    rank: 2,
    starts: new ulong[] { 2, 1 },
    strides: new ulong[] { 35, 17 },
    counts: new ulong[] { 2, 1 },
    blocks: new ulong[] { 30, 15 }
);

var result = dataset
    .Read<int>(
        fileSelection: datasetSelection,
        memorySelection: memorySelection,
        memoryDims: memoryDims
    )
    .ToArray2D(75, 25);
``` 

**Delegate selection**

A delegate accepts a custom walker function which select blocks of data at certain coordinates. Here is an example which selects a total number of 11 elements from a 3-dimensional dataset:

```cs
static IEnumerable<Step> Walker(ulong[] datasetDimensions)
{
    yield return new Step() { Coordinates = new ulong[] { 00, 00, 00 }, ElementCount = 1 };
    yield return new Step() { Coordinates = new ulong[] { 00, 05, 10 }, ElementCount = 5 };
    yield return new Step() { Coordinates = new ulong[] { 12, 01, 10 }, ElementCount = 2 };
    yield return new Step() { Coordinates = new ulong[] { 05, 07, 09 }, ElementCount = 3 };
};

var selection = new DelegateSelection(totalElementCount: 11, Walker);
```

## 4.3 Experimental: IQueryable (1-dimensional data only)

Another way to build the file selection is to invoke the `AsQueryable` method which can then be used as follows:

```cs
var result = dataset.AsQueryable<int>()
    .Skip(5)    // start
    .Stride(5)  // stride
    .Repeat(2)  // count
    .Take(3)    // block
    .ToArray();
```

All methods are optional, i.e. the code

```cs
var result = dataset.AsQueryable<int>()
    .Skip(5)
    .ToArray();
```

will simply skip the first 5 elements and return the rest of the dataset.

This way of building a hyperslab / selection has been implemented in an efford to provide a more .NET-like experience when working with data.

# 5. Filters

## 5.1 Built-in Filters
- Shuffle (hardware accelerated<sup>1</sup>, SSE2/AVX2)
- Fletcher32
- Deflate (zlib)
- Scale-Offset

<sup>1</sup> NET Standard 2.1 and above

## 5.2 External Filters
Before you can use external filters, you need to register them using ```H5Filter.Register(...)```. This method accepts a filter identifier, a filter name and the actual filter function.

This function could look like the following and should be adapted to your specific filter library:

```cs
public static FilterFunc MyFilterFunc { get; } = (flags, parameters, buffer) =>
{
    // Decompressing
    if (flags.HasFlag(H5FilterFlags.Decompress))
    {
        // pseudo code
        byte[] decompressedData = MyFilter.Decompress(parameters, buffer.Span);
        return decompressedData;
    }
    // Compressing
    else
    {
        throw new Exception("Writing data chunks is not yet supported by PureHDF.");
    }
};

```

## 5.3 Supported External Filters

**Deflate (Intel ISA-L)**

- based on [Intrinsics.ISA-L.PInvoke](https://www.nuget.org/packages/Intrinsics.ISA-L.PInvoke/)
- hardware accelerated: `SSE2` / `AVX2` / `AVX512`
- [benchmark results](https://github.com/Apollo3zehn/PureHDF/tree/master/benchmarks/PureHDF.Benchmarks/InflateComparison.md)

`dotnet add package PureHDF.Filters.Deflate.ISA-L`

```cs
using PureHDF.Filters;

H5Filter.Register(
    identifier: H5FilterID.Deflate, 
    name: "deflate", 
    filterFunction: H5DeflateISAL.FilterFunction);
```

**Deflate (SharpZipLib)**

- based on [SharpZipLib](https://www.nuget.org/packages/SharpZipLib)

`dotnet add package PureHDF.Filters.Deflate.SharpZipLib`

```cs
using PureHDF.Filters;

H5Filter.Register(
    identifier: H5FilterID.Deflate, 
    name: "deflate", 
    filterFunction: H5DeflateSharpZipLib.FilterFunction);
```

**Blosc / Blosc2**

- based on [Blosc2.PInvoke](https://www.nuget.org/packages/Blosc2.PInvoke)
- hardware accelerated: `SSE2` / `AVX2`

`dotnet add package PureHDF.Filters.Blosc2`

```cs
using PureHDF.Filters;

H5Filter.Register(
    identifier: (H5FilterID)32001, 
    name: "blosc2", 
    filterFunction: H5Blosc2.FilterFunction);
```

**BZip2 (SharpZipLib)**

- based on [SharpZipLib](https://www.nuget.org/packages/SharpZipLib)

`dotnet add package PureHDF.Filters.BZip2.SharpZipLib`

```cs
using PureHDF.Filters;

H5Filter.Register(
    identifier: (H5FilterID)307, 
    name: "bzip2", 
    filterFunction: H5BZip2SharpZipLib.FilterFunction);
```

**LZF**

`dotnet add package PureHDF.Filters.LZF`

```cs
using PureHDF.Filters;

H5Filter.Register(
    identifier: (H5FilterID)32000,
    name: "lzf", 
    filterFunction: H5Lzf.FilterFunction);
```

# 6. Reading Compound Data

There are three ways to read structs which are explained in the following sections. Here is an overview:

| method                | return value                 | speed | restrictions |
|-----------------------|------------------------------|-------|--------------|
| `Read<T>()`           | `T`                          | fast  | predefined type with correct field offsets required; nullable fields are not allowed |
| `ReadCompound<T>()`   | `T`                          | slow  | predefined type with matching names required |
| `ReadCompound()`      | `Dictionary<string, object>` | slow  | - |

## 6.1 Structs without nullable fields

Structs without any nullable fields (i.e. no strings and other reference types) can be read like any other dataset using a high performance copy operation:

```cs
[StructLayout(LayoutKind.Explicit, Size = 5)]
struct SimpleStruct
{
    [FieldOffset(0)]
    public byte ByteValue;

    [FieldOffset(1)]
    public ushort UShortValue;

    [FieldOffset(3)]
    public TestEnum EnumValue;
}

var compoundData = dataset.Read<SimpleStruct>();
```

Just make sure the field offset attributes matches the field offsets defined in the HDF5 file when the dataset was created.

*This method does not require that the structs field names match since they are simply mapped by their offset.*

If your struct contains an array of fixed size (here: `3`), you would need to add the `unsafe` modifier to the struct definition and define the struct as follows:


```cs
[StructLayout(LayoutKind.Explicit, Size = 8)]
unsafe struct SimpleStructWithArray
{
    // ... all the fields from the struct above, plus:

    [FieldOffset(5)]
    public fixed float FloatArray[3];
}

var compoundData = dataset.Read<SimpleStruct>();
```

## 6.2 Structs with nullable fields (strings, arrays)

If you have a struct with `string` or normal `array` fields, you need to use the slower `ReadCompound` method:

```cs
struct NullableStruct
{
    public float FloatValue;
    public string StringValue1;
    public string StringValue2;
    public byte ByteValue;
    public short ShortValue;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] FloatArray;
}

var compoundData = dataset.ReadCompound<NullableStruct>();
var compoundData = attribute.ReadCompound<NullableStruct>();
```

- Please note the use of the `MarshalAs` attribute on the array property. This attribute tells the runtime that this array is of fixed size (here: `3`) and that it should be treated as value which is embedded into the struct instead of being a separate object.

- Nested structs **with nullable fields** are not supported with this method.

- Arrays **with nullable element type** are not supported with this method.

- It is mandatory that the field names match exactly those in the HDF5 file. If you would like to use custom field names, consider the following approach:

```cs

// Apply the H5NameAttribute to the field with custom name.
struct NullableStructWithCustomFieldName
{
    [H5Name("FloatValue")]
    public float FloatValueWithCustomName;

    // ... more fields
}

// Create a name translator.
Func<FieldInfo, string> converter = fieldInfo =>
{
    var attribute = fieldInfo.GetCustomAttribute<H5NameAttribute>(true);
    return attribute is not null ? attribute.Name : fieldInfo.Name;
};

// Use that name translator.
var compoundData = dataset.ReadCompound<NullableStructWithCustomFieldName>(converter);
```

## 6.3 Unknown structs

You have no idea how the struct in the H5 file looks like? Or it is so large that it is no fun to predefine it? In that case, you can fall back to the non-generic `dataset.ReadCompound()` which returns a `Dictionary<string, object?>[]` where the dictionary values can be anything from simple value types to arrays or nested dictionaries (or even `H5ObjectReference`), depending on the kind of data in the file. Use the standard .NET dictionary methods to work with these kind of data.

The type mapping is as follows:

| H5 type                        | .NET type                    |
|--------------------------------|------------------------------|
| fixed point, 1 byte,  unsigned | `byte`                       |
| fixed point, 1 byte,    signed | `sbyte`                      |
| fixed point, 2 bytes, unsigned | `ushort`                     |
| fixed point, 2 bytes,   signed | `short`                      |
| fixed point, 4 bytes, unsigned | `uint`                       |
| fixed point, 4 bytes,   signed | `int`                        |
| fixed point, 8 bytes, unsigned | `ulong`                      |
| fixed point, 8 bytes,   signed | `long`                       |
| floating point, 4 bytes        | `float `                     |
| floating point, 8 bytes,       | `double`                     |
| string                         | `string`                     |
| bitfield                       | `byte[]`                     |
| opaque                         | `byte[]`                     |
| compound                       | `Dictionary<string, object?>` |
| reference                      | `H5ObjectReference`          |
| enumerated                     | `<base type>`                |
| variable length, type = string | `string`                     |
| array                          | `<base type>[]`              |

Not supported data types like `time` and `variable length type = sequence` will be represented as `null`.

## 7 Reading Multidimensional Data

### 7.1 Generic Method

Sometimes you want to read the data as multidimensional arrays. In that case use one of the `T[]` overloads like `ToArray3D` (there are overloads up to 6D). Here is an example:

```cs
var data3D = dataset
    .Read<int>()
    .ToArray3D(new long[] { -1, 7, 2 });
```

The methods accepts a `long[]` with the new array dimensions. This feature works similar to Matlab's [reshape](https://de.mathworks.com/help/matlab/ref/reshape.html) function. A slightly adapted citation explains the behavior:
> When you use `-1` to automatically calculate a dimension size, the dimensions that you *do* explicitly specify must divide evenly into the number of elements in the input array.

### 7.2 High-Performance Method (2D only)

The previously shown method (`ToArrayXD`) performs a copy operation. If you would like to avoid this, you might find the `Span2D` type interesting which is part of the CommunityToolkit.HighPerformance. To make use of it, run `dotnet add package CommunityToolkit.HighPerformance` and then use it like this:

```cs
using CommunityToolkit.HighPerformance;

data2D = dataset
    .Read<int>()
    .AsSpan()
    .AsSpan2D(height: 20, width: 10);
```

No data is being copied and you can work with the array similar to a normal `Span<T>`, i.e. you may want to [slice](https://learn.microsoft.com/en-us/windows/communitytoolkit/high-performance/span2d) through it.

# 8 Concurrency

Reading data from a dataset is thread-safe in the following cases, depending on the type of `H5File` constructor method you used:

|         | Open(`string`) | Open(`MemoryMappedViewAccessor`) | Open(`Stream`)                    |
| ------- | -------------- | -------------------------------- | --------------------------------- |
| .NET 4+ | x              | ✓                               | x                                 |
| .NET 6+ | ✓             | ✓                               | ✓ (if: `Stream` is `FileStream` or `AmazonS3Stream`) |

> The multi-threading support comes without significant usage of locking. Currently only the global heap cache uses thread synchronization primitives.

> Currently the default `SimpleChunkCache` is not thread safe and therefore every read operation must use its own cache (which is the default). This will be solved in a future release.

## 8.1 Multi-Threading (Memory-Mapped File)

If you have opened a file as memory-mapped file, you may read the data in parallel like this:

```cs
const ulong TOTAL_ELEMENT_COUNT = xxx;
const ulong SEGMENT_COUNT = xxx;
const ulong SEGMENT_SIZE = TOTAL_ELEMENT_COUNT / SEGMENT_COUNT;

using var mmf = MemoryMappedFile.CreateFromFile(FILE_PATH);
using var accessor = mmf.CreateViewAccessor();
using var file = H5File.Open(accessor);

var dataset = file.Dataset("xxx");
var buffer = new float[TOTAL_ELEMENT_COUNT];

Parallel.For(0, SEGMENT_COUNT, i =>
{
    var start = i * SEGMENT_SIZE;
    var partialBuffer = buffer.Slice(start, length: SEGMENT_SIZE);
    var fileSelection = new HyperslabSelection(start, block: SEGMENT_SIZE)

    dataset.Read<float>(partialBuffer, fileSelection);
});

```

## 8.2 Multi-Threading (FileStream) (.NET 6+)

Starting with .NET 6, there is a new API to access files in a thread-safe way which PureHDF utilizes. The process to load data in parallel is similar to the memory-mapped file approach above:

```cs
const ulong TOTAL_ELEMENT_COUNT = xxx;
const ulong SEGMENT_COUNT = xxx;
const ulong SEGMENT_SIZE = TOTAL_ELEMENT_COUNT / SEGMENT_COUNT;

using var file = H5File.OpenRead(FILE_PATH);

var dataset = file.Dataset("xxx");
var buffer = new float[TOTAL_ELEMENT_COUNT];

Parallel.For(0, SEGMENT_COUNT, i =>
{
    var start = i * SEGMENT_SIZE;
    var partialBuffer = buffer.Slice(start, length: SEGMENT_SIZE);
    var fileSelection = new HyperslabSelection(start, block: SEGMENT_SIZE)

    dataset.Read<float>(partialBuffer, fileSelection);
});

```

## 8.3 Async (.NET 6+)

PureHDF supports reading data asynchronously to allow the CPU work on other tasks while waiting for the result.

>Note: All `async` methods shown below are only truly asynchronous if the [FileStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.-ctor?view=net-7.0#system-io-filestream-ctor(system-string-system-io-filemode-system-io-fileaccess-system-io-fileshare-system-int32-system-boolean)) is opened with the `useAsync` parameter set to `true`:

```cs
var h5File = H5File.Open(
    filePath,
    FileMode.Open, 
    FileAccess.Read, 
    FileShare.Read, 
    useAsync: true);

// alternative
var stream = new FileStream(..., useAsync: true);
var h5File = H5File.Open(stream);
```

**Sample 1: Load data of two datasets**

```cs
async Task LoadDataAsynchronously()
{
    var data1Task = dataset1.ReadAsync<int>();
    var data2Task = dataset2.ReadAsync<int>();

    await Task.WhenAll(data1Task, data2Task);
}
```

**Sample 2: Load data of two datasets and process it**

```cs
async Task LoadAndProcessDataAsynchronously()
{
    var processedData1Task = Task.Run(async () => 
    {
        var data1 = await dataset1.ReadAsync<int>();
        ProcessData(data1);
    });

    var processedData2Task = Task.Run(async () => 
    {
        var data2 = await dataset2.ReadAsync<int>();
        ProcessData(data2);
    });

    await Task.WhenAll(processedData1Task, processedData2Task);
}
```

**Sample 3: Load data of a single dataset and process it**

```cs

async Task LoadAndProcessDataAsynchronously()
{
    var processedData1Task = Task.Run(async () => 
    {
        var fileSelection1 = new HyperslabSelection(start: 0, block: 50);
        var data1 = await dataset1.ReadAsync<int>(fileSelection1);

        ProcessData(data1);
    });

    var processedData2Task = Task.Run(async () => 
    {
        var fileSelection2 = new HyperslabSelection(start: 50, block: 50);
        var data2 = await dataset2.ReadAsync<int>(fileSelection2);

        ProcessData(data2);
    });

    await Task.WhenAll(processedData1Task, processedData2Task);
}
```

# 9 Amazon S3
| Requires             |
| -------------------- |
| `.NET Standard 2.1+` |

`dotnet add package PureHdf.VFD.AmazonS3`

```cs
using PureHDF.VFD.AmazonS3

// adapt to your needs
var credentials = new AnonymousAWSCredentials();
var region = RegionEndpoint.EUWest1;
var client = new AmazonS3Client(credentials, region);
var s3Stream = new AmazonS3Stream(client, bucketName: "xxx", key: "yyy");

using var file = H5File.Open(s3Stream);
...
```

> Note: The `AmazonS3Stream` caches S3 responses in cache slots of 1 MB by default (use the constructor overload to customize this). Data read from datasets is not being cached to keep the cache small but still useful.

# 10 Highly Scalable Data Service (HSDS)
| Requires  |
| --------- |
| `.NET 6+` |

This `HsdsConnector` shown below uses the `HsdsClient` from [this](https://github.com/Apollo3zehn/hsds-api) project. Please follow that link for information about authentication.

```cs
var domainName = "/shared/tall.h5";
var client = new HsdsClient(new Uri("http://hsdshdflab.hdfgroup.org"));
var root = HsdsConnector.Create(domainName, client);
var group = root.Group("/my-group");
// continue here as usual
...
```

> Note: The following features are not (yet) implemented:
- `IH5Attribute`
  - Read compounds
  - Read strings and other variable-length data types
- `IH5Dataset`:
  - Read compounds
  - Read strings and other variable-length data types
  - Memory selections
- `IH5DataType`:
  - `Size` property (the HSDS REST API does not seem to provide that information)
  - data type properties other than `integer`, `float` and `compound`
- `IH5FillValue`

**Please file a new issue if you encounter any problems.**

# 11 Intellisense

| Requires  |
| --------- |
| `.NET 5+` |

## 11.1 Introduction

Consider the following H5 file:

![HDF View](https://github.com/Apollo3zehn/PureHDF/raw/master/doc/images/hdfview.png)

If you would like to access `sub_dataset2` you would normally do

```cs
    using var h5File = H5File.OpenRead(FILE_PATH);
    var dataset = h5File.Group("group1").Dataset("sub_dataset2");
```

When you have files with a large number of groups or a deep hierarchy and you often need to work on different paths within the file, it could very useful to get intellisense support from your favourite IDE which helps you navigating through the file.

PureHDF utilizes the source generator feature introduced with .NET 5 which allows to generate additional code during compilation. The generator, which comes with the `PureHDF.SourceGenerator` package, enables you to interact with the H5 file like this:

```cs
var dataset = bindings.group1.sub_dataset2;
```

## 11.2 Getting Started

Run the following command:

```bash
dotnet add package PureHDF.SourceGenerator
dotnet restore
```

> Note: Make sure that all project dependencies are restored before you continue.

Then define the path to your H5 file from which the bindings should be generated and use it in combination with the `H5SourceGenerator` attribute:

```cs
using PureHDF;

[H5SourceGenerator(filePath: Program.FILE_PATH)]
internal partial class MyGeneratedH5Bindings {};

static class Program
{
    public const string FILE_PATH = "myFile.h5";

    static void Main()
    {
        using var h5File = H5File.OpenRead(FILE_PATH);
        var bindings = new MyGeneratedH5Bindings(h5File);
        var myDataset = bindings.group1.sub_dataset2;
    }
}
```

Your IDE should now run the source generator behind the scenes and you should be able to get intellisense support:

![Intellisense](https://github.com/Apollo3zehn/PureHDF/raw/master/doc/images/intellisense.png)

In case you do not want to access the dataset but the parent group instead, use the `Get()` method like this:

```cs
var myGroup = bindings.group1.Get();
```

> Note: Invalid characters like spaces will be replaced by underscores.

# 12 Unsupported Features

The following features are **not** (yet) supported:

- Virtual datasets with **unlimited dimensions**
- Filters: `N-bit`, `SZIP`

# 13 Comparison Table

The following table considers only projects listed on Nuget.org.

|         Name                                                                      | Arch    | Platform    | Kind     | Mode | Version   | License     | Maintainer         | Comment              |  
| --------------------------------------------------------------------------------- | ------- | ----------- | -------- | ---- | --------- | ----------- | ------------------ | -------------------- |  
| **v1.10**                                                                         |         |             |          |      |           |             |                    |                      |  
| [PureHDF](https://www.nuget.org/packages/PureHDF)                                 | all     | all         | managed  | ro   | 1.10.*    | MIT         | Apollo3zehn        |                      |
| [HDF5-CSharp](https://www.nuget.org/packages/HDF5-CSharp)                         | x86,x64 | Win,Lin,Mac | HL       | rw   | 1.10.6    | MIT         | LiorBanai          |                      |  
| [SciSharp.Keras.HDF5](https://www.nuget.org/packages/SciSharp.Keras.HDF5)         | x86,x64 | Win,Lin,Mac | HL       | rw   | 1.10.5    | MIT         | SciSharp           | fork of HDF-CSharp   |  
| [ILNumerics.IO.HDF5](https://www.nuget.org/packages/ILNumerics.IO.HDF5)           | x64     | Win,Lin     | HL       | rw   | ?         | proprietary | IL\_Numerics\_GmbH | probably 1.10        |  
| [LiteHDF](https://www.nuget.org/packages/LiteHDF)                                 | x86,x64 | Win,Lin,Mac | HL       | ro   | 1.10.5    | MIT         | silkfire           |                      |  
| [hdflib](https://www.nuget.org/packages/hdflib)                                   | x86,x64 | Windows     | HL       | wo   | 1.10.6    | MIT         | bdebree            |                      |  
| [Mbc.Hdf5Utils](https://www.nuget.org/packages/Mbc.Hdf5Utils)                     | x86,x64 | Win,Lin,Mac | HL       | rw   | 1.10.6    | Apache-2.0  | bqstony            |                      |  
| [HDF.PInvoke](https://www.nuget.org/packages/HDF.PInvoke)                         | x86,x64 | Windows     | bindings | rw   | 1.8,1.10.6| HDF5        | hdf,gheber         |                      |  
| [HDF.PInvoke.1.10](https://www.nuget.org/packages/HDF.PInvoke.1.10)               | x86,x64 | Win,Lin,Mac | bindings | rw   | 1.10.6    | HDF5        | hdf,Apollo3zehn    |                      |  
| [HDF.PInvoke.NETStandard](https://www.nuget.org/packages/HDF.PInvoke.NETStandard) | x86,x64 | Win,Lin,Mac | bindings | rw   | 1.10.5    | HDF5        | surban             |                      |  
| **v1.8**                                                                          |         |             |          |      |           |             |                    |                      |  
| [HDF5DotNet.x64](https://www.nuget.org/packages/HDF5DotNet.x64)                   | x64     | Windows     | HL       | rw   | 1.8       | HDF5        | thieum             |                      |  
| [HDF5DotNet.x86](https://www.nuget.org/packages/HDF5DotNet.x86)                   | x86     | Windows     | HL       | rw   | 1.8       | HDF5        | thieum             |                      |  
| [sharpHDF](https://www.nuget.org/packages/sharpHDF)                               | x64     | Windows     | HL       | rw   | 1.8       | MIT         | bengecko           |                      |  
| [HDF.PInvoke](https://www.nuget.org/packages/HDF.PInvoke)                         | x86,x64 | Windows     | bindings | rw   | 1.8,1.10.6| HDF5        | hdf,gheber         |                      |  
| [hdf5-v120-complete](https://www.nuget.org/packages/hdf5-v120-complete)           | x86,x64 | Windows     | native   | rw   | 1.8       | HDF5        | daniel.gracia      |                      |  
| [hdf5-v120](https://www.nuget.org/packages/hdf5-v120)                             | x86,x64 | Windows     | native   | rw   | 1.8       | HDF5        | keen               |                      |  

**Abbreviations:**

| Term      | .NET API   | Native dependencies |
| --------- | ---------- | ------------------- |
| `managed` | high-level | none                |
| `HL`      | high-level | C-library           |
| `bindings`| low-level  | C-library           |
| `native`  | none       | C-library           |
