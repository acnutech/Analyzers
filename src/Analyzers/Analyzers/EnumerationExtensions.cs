using System;
using System.Collections.Generic;
using System.Linq;

namespace Acnutech.Analyzers
{
    internal static class EnumerationExtensions
    {
        /// <summary>
        /// Returns the single element of a collection if it contains one element, otherwise returns the default value.
        /// </summary>
        /// <typeparam name="T">Represents the type of elements in the collection being evaluated.</typeparam>
        /// <param name="source">The collection of elements to be examined for a single item.</param>
        /// <returns>The single element if exactly one exists, or the default value if none or multiple elements are found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the provided collection is null.</exception>
        public static T SingleOrDefaultIfMultiple<T>(this IEnumerable<T> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if(source is ICollection<T> collection)
            {
                if (collection.Count > 1)
                {
                    return default;
                }
                return collection.FirstOrDefault();
            }

            using (var enumerator = source.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                {
                    return default;
                }
                var result = enumerator.Current;
                if (enumerator.MoveNext())
                {
                    return default;
                }
                return result;
            }
        }
    }
}
