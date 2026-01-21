using System.Threading.Tasks;

namespace NeuroEngine.Core
{
    /// <summary>
    /// Interface for writing persistent state to the hooks/ directory.
    /// This is Layer 4 (Persistence) - agents need memory across sessions.
    /// </summary>
    public interface IHooksWriter
    {
        /// <summary>
        /// Write data to a hooks subdirectory.
        /// </summary>
        /// <param name="category">Subdirectory (scenes, tasks, validation, etc.)</param>
        /// <param name="filename">File name (without path)</param>
        /// <param name="data">Object to serialize as JSON</param>
        Task WriteAsync(string category, string filename, object data);

        /// <summary>
        /// Read data from a hooks subdirectory.
        /// </summary>
        /// <typeparam name="T">Type to deserialize to</typeparam>
        /// <param name="category">Subdirectory</param>
        /// <param name="filename">File name</param>
        /// <returns>Deserialized object or default</returns>
        Task<T> ReadAsync<T>(string category, string filename);

        /// <summary>
        /// Check if a file exists in hooks.
        /// </summary>
        bool Exists(string category, string filename);
    }
}
