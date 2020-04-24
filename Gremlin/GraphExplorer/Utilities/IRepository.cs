namespace GraphExplorer.Utilities
{
    using System.Threading.Tasks;

    internal interface IRepository<T>
    {
        Task<T> GetItemAsync(string collectionId);
        Task CreateOrUpdateItemAsync(T item, string collectionId);
        Task DeleteItemAsync(string id, string collectionId);
    }
}
