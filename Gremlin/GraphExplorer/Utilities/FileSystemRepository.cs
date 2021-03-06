﻿using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GraphExplorer.Utilities
{
    public class FileSystemRepository<T> : IRepository<T>
    {
        private string _fullFilePath = string.Empty;
        private string _defaultItem = "__defaultItem";

        Dictionary<string, T> _allItems;
        private object _theLock = new object();

        public FileSystemRepository(IWebHostEnvironment env, string filename)
        {
            _fullFilePath = Path.Combine(env.ContentRootPath, "data", filename);
            _allItems = ReadAllItems();
        }

        public async Task<T> GetItemAsync(string collectionId)
        {
            if (!_allItems.TryGetValue(collectionId, out T item))
            {
                //create a new T and put default items in it
                await CreateOrUpdateItemAsync(_allItems[_defaultItem], collectionId);
                item = _allItems[_defaultItem];
            }

            return await Task.FromResult<T>(item);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task CreateOrUpdateItemAsync(T item, string collectionId)
        {
            // add (or overwrite) item for this collection
            _allItems[collectionId] = item;
            SaveToRepository();
        }

        public async Task DeleteItemAsync(string id, string collectionId)
        {
            if (_allItems.ContainsKey(collectionId))
            {
                _allItems.Remove(collectionId);
                SaveToRepository();
            }
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

        private Dictionary<string, T> ReadAllItems()
        {
            string json = string.Empty;
            if (File.Exists(_fullFilePath))
            {
                json = File.ReadAllText(_fullFilePath);
            }

            if (!string.IsNullOrEmpty(json))
            {
                return JsonConvert.DeserializeObject<Dictionary<string, T>>(json);
            }
            else
            {
                return new Dictionary<string, T>();
            }
        }

        private void SaveToRepository()
        {
            lock (_theLock)
            {
                using (FileStream fs = new FileStream(_fullFilePath, FileMode.Create))
                {
                    using (StreamWriter streamWriter = new StreamWriter(fs))
                    {
                        using (JsonWriter jw = new JsonTextWriter(streamWriter))
                        {
                            jw.Formatting = Formatting.Indented;

                            JsonSerializer serializer = new JsonSerializer();
                            serializer.Serialize(jw, _allItems);
                        }
                    }
                }
            }
        }
    }
}
