using System;
using System.Collections.Generic;

namespace CSUtil.Commons {
    public static class CollectionExtensions {

        public static void AddRange2<T>(this ICollection<T> target, IEnumerable<T> items) {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (items == null) {
                return;
            }
            foreach (var element in items)
                target.Add(element);
        }

        public static void RemoveRange<T>(this ICollection<T> target, IEnumerable<T> items) {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (items == null) {
                return;
            }
            foreach (var element in items)
                target.Remove(element);
        }
    }
}