namespace NuGetGallery
{
	public interface ICache
	{
		void Add(string key, object value);
		object Get(string key);
		void Remove(string key);
	}
}