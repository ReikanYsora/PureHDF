﻿using System.Collections.Concurrent;

namespace PureHDF;

internal static class NativeCache
{
    #region Constructors

    static NativeCache()
    {
        _globalHeapMap = new ConcurrentDictionary<Superblock, Dictionary<ulong, GlobalHeapCollection>>();
        _fileMap = new ConcurrentDictionary<Superblock, Dictionary<string, NativeFile>>();
    }

    #endregion

    #region Shared

    public static void Clear(Superblock superblock)
    {
        // global heap
        if (_globalHeapMap.ContainsKey(superblock))
            _globalHeapMap.TryRemove(superblock, out var _);


        // file map
        if (_fileMap.ContainsKey(superblock))
        {
            var pathToH5FileMap = _fileMap[superblock];

            foreach (var h5File in pathToH5FileMap.Values)
            {
                h5File.Dispose();
            }

            _fileMap.TryRemove(superblock, out var _);
        }
    }

    #endregion

    #region Global Heap

    private static readonly ConcurrentDictionary<Superblock, Dictionary<ulong, GlobalHeapCollection>> _globalHeapMap;

    public static GlobalHeapCollection GetGlobalHeapObject(NativeContext context, ulong address)
    {
        var (_, superblock) = context;

        if (!_globalHeapMap.TryGetValue(superblock, out var addressToCollectionMap))
        {
            addressToCollectionMap = new Dictionary<ulong, GlobalHeapCollection>();
            _globalHeapMap.AddOrUpdate(superblock, addressToCollectionMap, (_, oldAddressToCollectionMap) => addressToCollectionMap);
        }

        if (!addressToCollectionMap.TryGetValue(address, out var collection))
        {
            collection = ReadGlobalHeapCollection(context, address);
            addressToCollectionMap[address] = collection;
        }

        return collection;
    }

    private static GlobalHeapCollection ReadGlobalHeapCollection(NativeContext context, ulong address)
    {
        context.Driver.Seek((long)address, SeekOrigin.Begin);
        return new GlobalHeapCollection(context);
    }

    #endregion

    #region File Handles

    private static readonly ConcurrentDictionary<Superblock, Dictionary<string, NativeFile>> _fileMap;

    public static NativeFile GetH5File(Superblock superblock, string absoluteFilePath, bool useAsync)
    {
        if (!Uri.TryCreate(absoluteFilePath, UriKind.Absolute, out var uri))
            throw new Exception("The provided path is not absolute.");

        if (!uri.IsFile && !uri.IsUnc)
            throw new Exception("The provided path is not a file path or a UNC path.");

        if (!_fileMap.TryGetValue(superblock, out var pathToH5FileMap))
        {
            pathToH5FileMap = new Dictionary<string, NativeFile>();
            _fileMap.AddOrUpdate(superblock, pathToH5FileMap, (_, oldPathToH5FileMap) => pathToH5FileMap);
        }

        if (!pathToH5FileMap.TryGetValue(uri.AbsoluteUri, out var h5File))
        {
            // TODO: This does not correspond to https://support.hdfgroup.org/HDF5/doc/RM/H5L/H5Lcreate_external.htm
            h5File = (NativeFile)H5File.Open(uri.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read, useAsync: useAsync);
            pathToH5FileMap[uri.AbsoluteUri] = h5File;
        }

        return h5File;
    }

    #endregion
}