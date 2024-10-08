﻿using Lemur.FS;
using System;

namespace Lemur.JS.Embedded {
    public class File_t : embedable
    {
        [ApiDoc("Read a file from a path. return its contents as a string")]
        public object? read(string path)
        {
            ArgumentNullException.ThrowIfNull(path, nameof(path));
            return FileSystem.Read(path);
        }
        [ApiDoc("Write a string of data to a file at path.")]
        public void write(string path, string data)
        {
            ArgumentNullException.ThrowIfNull(path, nameof(path));
            ArgumentNullException.ThrowIfNull(data, nameof(data));
            FileSystem.Write(path, data);
        }
        [ApiDoc("Return a bool indicating whether a file exists.")]
        public bool exists(string path)
        {
            ArgumentNullException.ThrowIfNull(path, nameof(path));
            return FileSystem.FileExists(path);
        }

        [ApiDoc("Resolve a path to the absolute canonical path")]
        public string canonicalPath(string path) {
            return FileSystem.GetResourcePath(path);
        }
    }
}

