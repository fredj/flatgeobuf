using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using NetTopologySuite.IO;

using FlatBuffers;
using FlatGeobuf;

namespace FlatGeobuf.GeoJson
{
    public static class FeatureCollection {
        public static byte[] ToFlatGeobuf(string geojson) {
            var reader = new GeoJsonReader();
            var fc = reader.Read<NetTopologySuite.Features.FeatureCollection>(geojson);

            if (fc.Features.Count == 0)
                throw new ApplicationException("Empty feature collection is not allowed as input");

            // TODO: make it optional to use first feature as column schema
            var featureFirst = fc.Features.First();
            Dictionary<string, ColumnType> columns = null;
            if (featureFirst.Attributes != null && featureFirst.Attributes.Count > 0)
            {
                columns = featureFirst.Attributes.GetNames()
                    .ToDictionary(n => n, n => ToColumnType(featureFirst.Attributes.GetType(n)));
            }

            var header = BuildHeader(fc, columns);

            var memoryStream = new MemoryStream();
            memoryStream.Write(header, 0, header.Length);

            foreach (var feature in fc.Features)
            {
                var buffer = FlatGeobuf.GeoJson.Feature.ToByteBuffer(feature, columns);
                memoryStream.Write(buffer, 0, buffer.Length);
            }
            
            return memoryStream.ToArray();
        }

        private static ColumnType ToColumnType(Type type) {
            switch (Type.GetTypeCode(type)) {
                case TypeCode.Int32: return ColumnType.INT;
                case TypeCode.Int64: return ColumnType.LONG;
                case TypeCode.Double: return ColumnType.DOUBLE;
                default: throw new ApplicationException("Unknown type");
            }
        }

        public static string FromFlatGeobuf(byte[] bytes) {
            var fc = new NetTopologySuite.Features.FeatureCollection();

            var bb = new FlatBuffers.ByteBuffer(bytes);
            
            var headerLength = ByteBufferUtil.GetSizePrefix(bb);
            bb.Position = FlatBufferConstants.SizePrefixLength;
            var header = Header.GetRootAsHeader(bb);

            IDictionary<string, ColumnType> columns = null;
            if (header.ColumnsLength > 0) {
                columns = new Dictionary<string, ColumnType>();
                for (int i = 0; i < header.ColumnsLength; i++) {
                    var column = header.Columns(i).Value;
                    columns.Add(column.Name, column.Type);
                }
            }

            var count = header.FeaturesCount;
            bb.Position += headerLength;

            while (count-- > 0) {
                var featureLength = ByteBufferUtil.GetSizePrefix(bb);
                bb.Position += FlatBufferConstants.SizePrefixLength;
                var feature = Feature.FromByteBuffer(bb, columns);
                fc.Add(feature);
                bb.Position += featureLength;
            }

            var writer = new GeoJsonWriter();
            var geojson = writer.Write(fc);
            return geojson;
        }

        private static byte[] BuildHeader(NetTopologySuite.Features.FeatureCollection fc, Dictionary<string, ColumnType> columns) {
            var builder = new FlatBufferBuilder(1024);

            // TODO: make it optional to use first feature as column schema
            var feature = fc.Features.First();
            VectorOffset? columnsOffset = null;
            if (columns != null) {
                var columnsArray = columns
                    .Select(c => Column.CreateColumn(builder, builder.CreateString(c.Key), c.Value))
                    .ToArray();
                columnsOffset = Column.CreateSortedVectorOfColumn(builder, columnsArray);
            }

            Header.StartHeader(builder);
            if (columnsOffset.HasValue)
                Header.AddColumns(builder, columnsOffset.Value);
            Header.AddFeaturesCount(builder, (ulong) fc.Features.Count);
            var offset = Header.EndHeader(builder);

            builder.FinishSizePrefixed(offset.Value);

            return builder.DataBuffer.ToSizedArray();
        }
    }
}