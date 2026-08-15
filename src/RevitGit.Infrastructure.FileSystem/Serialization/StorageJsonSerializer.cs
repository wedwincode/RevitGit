using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using RevitGit.Infrastructure.FileSystem.Exceptions;

namespace RevitGit.Infrastructure.FileSystem.Serialization
{
    internal sealed class StorageJsonSerializer
    {
        public byte[] Serialize<T>(T value)
        {
            try
            {
                using (var stream = new MemoryStream())
                {
                    CreateSerializer<T>().WriteObject(stream, value);
                    return stream.ToArray();
                }
            }
            catch (Exception exception) when (
                exception is SerializationException
                || exception is InvalidDataContractException)
            {
                throw new StorageException("Storage data could not be serialized.", exception);
            }
        }

        public T Deserialize<T>(byte[] content, string description)
        {
            try
            {
                using (var stream = new MemoryStream(content))
                {
                    return (T)CreateSerializer<T>().ReadObject(stream);
                }
            }
            catch (Exception exception) when (
                exception is SerializationException
                || exception is XmlException
                || exception is InvalidDataContractException)
            {
                throw new RepositoryCorruptedException(description + " contains malformed JSON.", exception);
            }
        }

        public string ToUtf8String<T>(T value)
        {
            return Encoding.UTF8.GetString(Serialize(value));
        }

        private static DataContractJsonSerializer CreateSerializer<T>()
        {
            return new DataContractJsonSerializer(
                typeof(T),
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true
                });
        }
    }
}
