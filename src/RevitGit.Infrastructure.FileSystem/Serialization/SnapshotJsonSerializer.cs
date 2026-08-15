using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using RevitGit.Domain.Exceptions;
using RevitGit.Domain.Snapshots;
using RevitGit.Infrastructure.FileSystem.Exceptions;

namespace RevitGit.Infrastructure.FileSystem.Serialization
{
    public sealed class SnapshotJsonSerializer : IFamilySnapshotSerializer
    {
        private readonly StorageJsonSerializer _serializer = new StorageJsonSerializer();

        public byte[] Serialize(FamilySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return _serializer.Serialize(ToDto(snapshot));
        }

        public FamilySnapshot Deserialize(byte[] content)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            var dto = _serializer.Deserialize<SnapshotDto>(content, "Snapshot metadata");
            try
            {
                return FromDto(dto);
            }
            catch (Exception exception) when (
                exception is DomainException
                || exception is ArgumentException
                || exception is NullReferenceException)
            {
                throw new RepositoryCorruptedException(
                    "Snapshot metadata has an unsupported or incomplete format.",
                    exception);
            }
        }

        private static SnapshotDto ToDto(FamilySnapshot snapshot)
        {
            var parameters = new List<ParameterDto>();
            foreach (var parameter in snapshot.Parameters)
            {
                parameters.Add(new ParameterDto
                {
                    StableKey = parameter.StableKey,
                    Name = parameter.Name,
                    DataType = (int)parameter.DataType,
                    Scope = (int)parameter.Scope,
                    Formula = parameter.Formula
                });
            }

            var types = new List<FamilyTypeDto>();
            foreach (var type in snapshot.Types)
            {
                var values = new List<ParameterValueEntryDto>();
                foreach (var pair in type.Values)
                {
                    values.Add(new ParameterValueEntryDto
                    {
                        ParameterStableKey = pair.Key,
                        Value = ToDto(pair.Value)
                    });
                }

                types.Add(new FamilyTypeDto { Name = type.Name, Values = values });
            }

            return new SnapshotDto
            {
                SchemaVersion = snapshot.SchemaVersion,
                FamilyName = snapshot.FamilyName,
                Category = snapshot.Category,
                Parameters = parameters,
                Types = types,
                GeometryFingerprint = snapshot.GeometryFingerprint
            };
        }

        private static ParameterValueDto ToDto(ParameterValue value)
        {
            return new ParameterValueDto
            {
                Kind = (int)value.Kind,
                StringValue = value.StringValue,
                IntegerValue = value.IntegerValue,
                DoubleValue = value.DoubleValue,
                BooleanValue = value.BooleanValue
            };
        }

        private static FamilySnapshot FromDto(SnapshotDto dto)
        {
            if (dto == null
                || dto.SchemaVersion != SnapshotSchema.CurrentVersion
                || dto.Parameters == null
                || dto.Types == null)
            {
                throw new RepositoryCorruptedException(
                    "Snapshot metadata has an unsupported or incomplete format.");
            }

            var parameters = new List<FamilyParameterSnapshot>();
            foreach (var parameter in dto.Parameters)
            {
                parameters.Add(new FamilyParameterSnapshot(
                    parameter.StableKey,
                    parameter.Name,
                    (ParameterDataType)parameter.DataType,
                    (ParameterScope)parameter.Scope,
                    parameter.Formula));
            }

            var types = new List<FamilyTypeSnapshot>();
            foreach (var type in dto.Types)
            {
                if (type.Values == null)
                {
                    throw new RepositoryCorruptedException("Snapshot family type values are missing.");
                }

                var values = new List<KeyValuePair<string, ParameterValue>>();
                foreach (var entry in type.Values)
                {
                    values.Add(new KeyValuePair<string, ParameterValue>(
                        entry.ParameterStableKey,
                        FromDto(entry.Value)));
                }

                types.Add(new FamilyTypeSnapshot(type.Name, values));
            }

            return new FamilySnapshot(
                dto.FamilyName,
                dto.Category,
                parameters,
                types,
                dto.GeometryFingerprint);
        }

        private static ParameterValue FromDto(ParameterValueDto dto)
        {
            if (dto == null)
            {
                throw new RepositoryCorruptedException("Snapshot parameter value is missing.");
            }

            switch ((ParameterValueKind)dto.Kind)
            {
                case ParameterValueKind.Null:
                    return ParameterValue.Null;
                case ParameterValueKind.String:
                    return ParameterValue.FromString(dto.StringValue);
                case ParameterValueKind.Integer:
                    if (!dto.IntegerValue.HasValue)
                    {
                        break;
                    }

                    return ParameterValue.FromInteger(dto.IntegerValue.Value);
                case ParameterValueKind.Double:
                    if (!dto.DoubleValue.HasValue)
                    {
                        break;
                    }

                    return ParameterValue.FromDouble(dto.DoubleValue.Value);
                case ParameterValueKind.Boolean:
                    if (!dto.BooleanValue.HasValue)
                    {
                        break;
                    }

                    return ParameterValue.FromBoolean(dto.BooleanValue.Value);
            }

            throw new RepositoryCorruptedException("Snapshot parameter value kind is invalid.");
        }

        [DataContract]
        private sealed class SnapshotDto
        {
            [DataMember(Name = "schemaVersion", Order = 1)] public int SchemaVersion { get; set; }
            [DataMember(Name = "familyName", Order = 2)] public string FamilyName { get; set; }
            [DataMember(Name = "category", Order = 3)] public string Category { get; set; }
            [DataMember(Name = "parameters", Order = 4)] public List<ParameterDto> Parameters { get; set; }
            [DataMember(Name = "types", Order = 5)] public List<FamilyTypeDto> Types { get; set; }
            [DataMember(Name = "geometryFingerprint", Order = 6, EmitDefaultValue = false)] public string GeometryFingerprint { get; set; }
        }

        [DataContract]
        private sealed class ParameterDto
        {
            [DataMember(Name = "stableKey", Order = 1)] public string StableKey { get; set; }
            [DataMember(Name = "name", Order = 2)] public string Name { get; set; }
            [DataMember(Name = "dataType", Order = 3)] public int DataType { get; set; }
            [DataMember(Name = "scope", Order = 4)] public int Scope { get; set; }
            [DataMember(Name = "formula", Order = 5, EmitDefaultValue = false)] public string Formula { get; set; }
        }

        [DataContract]
        private sealed class FamilyTypeDto
        {
            [DataMember(Name = "name", Order = 1)] public string Name { get; set; }
            [DataMember(Name = "values", Order = 2)] public List<ParameterValueEntryDto> Values { get; set; }
        }

        [DataContract]
        private sealed class ParameterValueEntryDto
        {
            [DataMember(Name = "parameterStableKey", Order = 1)] public string ParameterStableKey { get; set; }
            [DataMember(Name = "value", Order = 2)] public ParameterValueDto Value { get; set; }
        }

        [DataContract]
        private sealed class ParameterValueDto
        {
            [DataMember(Name = "kind", Order = 1)] public int Kind { get; set; }
            [DataMember(Name = "stringValue", Order = 2, EmitDefaultValue = false)] public string StringValue { get; set; }
            [DataMember(Name = "integerValue", Order = 3, EmitDefaultValue = false)] public long? IntegerValue { get; set; }
            [DataMember(Name = "doubleValue", Order = 4, EmitDefaultValue = false)] public double? DoubleValue { get; set; }
            [DataMember(Name = "booleanValue", Order = 5, EmitDefaultValue = false)] public bool? BooleanValue { get; set; }
        }
    }
}
