using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace GenericOperations.EF
{
    public class EntityOperations
    {
        // merge all dependent relationships from a given source object to a destination object, then optionally remove the source
        // accepts a source and destination entity of the same type, as well as an optionally specified list of relationship names and an optional flag to preserve the source object
        public static void Merge<T>(DbContext context, T source, T destination, List<string> relationshipsToMerge = null, bool preserveSource = false)
        {
            if (relationshipsToMerge == null) relationshipsToMerge = EntityOperations.GetDependentPropertyInfo(source); // grab all dependent relationships if not explicitly specified

            foreach (var relationship in relationshipsToMerge)
            {
                var collection = EntityOperations.ResolveRelationship(context, source, relationship);
                if (collection == null) continue; // relationship doesn't exist, skip

                foreach (var dependent in EntityOperations.ExtractToList((IEnumerable)collection.CurrentValue))
                {
                    context.Entry(dependent).Reference(source.GetType().Name).CurrentValue = destination; // remap each dependent entity to destination object
                }
            }

            if (!preserveSource)
            {
                EntityOperations.Delete(context, source); // remove source object and unmerged dependencies
            }

            context.SaveChanges();
        }

        // delete entity and dependent relationships
        public static void Delete(DbContext context, object entity)
        {
            var relationshipsToDelete = EntityOperations.GetDependentPropertyInfo(entity); // grab all dependent relationships

            foreach (var relationship in relationshipsToDelete)
            {
                var collection = EntityOperations.ResolveRelationship(context, entity, relationship);
                if (collection == null) continue; // relationship doesn't exist, skip

                foreach (var dependent in EntityOperations.ExtractToList((IEnumerable)collection.CurrentValue))
                {
                    context.Entry(dependent).State = EntityState.Deleted; // remove dependent entity
                }
            }

            context.Entry(entity).State = EntityState.Deleted; // remove original entity
        }

        // grabs a list of names of all public collection properties from an object, these are expected to represent dependent relationships for an entity
        // properties matching critera but not representing a dependent relationship are expected to be culled by ResolveRelationship
        public static List<string> GetDependentPropertyInfo(object entity)
        {
            var properties = entity.GetType().GetProperties().ToList();

            properties = properties.Where(o => typeof(IEnumerable).IsAssignableFrom(o.PropertyType) && o.PropertyType != typeof(string)).ToList(); // grab all properties implementing IEnumerable, excluding string

            return properties.Select(o => o.Name).ToList(); // resolve to string
        }

        // safely resolve a potential relationship by name
        private static System.Data.Entity.Infrastructure.DbCollectionEntry ResolveRelationship(DbContext context, object source, string relationship)
        {
            try
            {
                var collection = context.Entry(source).Collection(relationship);
                if (!collection.IsLoaded) collection.Load();

                return collection;
            }
#pragma warning disable CS0168 // suppress 'variable is declared but never used' warning
            catch (ArgumentException ex)
#pragma warning restore CS0168
            {
                return null; // relationship doesn't exist
            }
            catch
            {
                throw;
            }
        }

        // helper function to resolve an IEnumerable to a freshly instantiated list
        private static List<object> ExtractToList(IEnumerable enumerable)
        {
            List<object> entries = new List<object>();
            foreach (var entry in enumerable) entries.Add(entry); // avoid collection was modified issues   

            return entries;
        }
    }
}