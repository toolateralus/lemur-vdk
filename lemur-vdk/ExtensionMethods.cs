﻿using Newtonsoft.Json.Linq;
using System.Linq;

namespace Lemur.Types {
    internal static class ExtensionMethods {
        public static bool JContains<T>(this JArray arr, T obj) {
            return arr.Any((e) => e.Value<T>() is T s && s.Equals(obj));
        }
    }
}