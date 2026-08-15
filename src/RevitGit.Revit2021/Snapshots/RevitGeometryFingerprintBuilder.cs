using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RevitGit.Revit2021.Snapshots
{
    internal sealed class RevitGeometryFingerprintBuilder
    {
        private readonly Options _options = new Options
        {
            ComputeReferences = false,
            IncludeNonVisibleObjects = true,
            DetailLevel = ViewDetailLevel.Fine
        };

        public string Build(Document document)
        {
            var descriptors = new List<string>();
            var elements = new FilteredElementCollector(document)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (var element in elements)
            {
                if (element is ReferencePlane || element is Dimension || element is View)
                {
                    continue;
                }

                var geometry = element.get_Geometry(_options);
                if (geometry == null)
                {
                    continue;
                }

                var elementDescriptors = new List<string>();
                AppendGeometry(geometry, Transform.Identity, elementDescriptors);
                var elementType = element.GetType().FullName;
                descriptors.AddRange(elementDescriptors.Select(value => elementType + "|" + value));
            }

            return CanonicalGeometryFingerprint.Compute(descriptors);
        }

        private static void AppendGeometry(
            GeometryElement geometry,
            Transform transform,
            ICollection<string> descriptors)
        {
            foreach (var geometryObject in geometry)
            {
                var instance = geometryObject as GeometryInstance;
                if (instance != null)
                {
                    AppendGeometry(instance.GetSymbolGeometry(), transform.Multiply(instance.Transform), descriptors);
                    continue;
                }

                var solid = geometryObject as Solid;
                if (solid != null)
                {
                    AppendSolid(solid, transform, descriptors);
                    continue;
                }

                var mesh = geometryObject as Mesh;
                if (mesh != null)
                {
                    AppendMesh(mesh, transform, "mesh", descriptors);
                    continue;
                }

                var curve = geometryObject as Curve;
                if (curve != null)
                {
                    AppendCurve(curve, transform, descriptors);
                    continue;
                }

                var polyLine = geometryObject as PolyLine;
                if (polyLine != null)
                {
                    AppendPointSequence("polyline", polyLine.GetCoordinates(), transform, descriptors);
                    continue;
                }

                var point = geometryObject as Point;
                if (point != null)
                {
                    descriptors.Add("point|" + CanonicalPoint(transform.OfPoint(point.Coord)));
                }
            }
        }

        private static void AppendSolid(
            Solid solid,
            Transform transform,
            ICollection<string> descriptors)
        {
            if (solid.Faces.Size == 0 && solid.Edges.Size == 0)
            {
                return;
            }

            descriptors.Add(
                "solid|volume=" + CanonicalGeometryFingerprint.Number(RevitUnitNormalizer.ToCubicMillimeters(solid.Volume))
                + "|area=" + CanonicalGeometryFingerprint.Number(RevitUnitNormalizer.ToSquareMillimeters(solid.SurfaceArea))
                + "|faces=" + solid.Faces.Size
                + "|edges=" + solid.Edges.Size);

            foreach (Face face in solid.Faces)
            {
                AppendMesh(face.Triangulate(), transform, "face", descriptors);
            }
        }

        private static void AppendMesh(
            Mesh mesh,
            Transform transform,
            string prefix,
            ICollection<string> descriptors)
        {
            for (var index = 0; index < mesh.NumTriangles; index++)
            {
                var triangle = mesh.get_Triangle(index);
                var points = new[]
                {
                    CanonicalPoint(transform.OfPoint(triangle.get_Vertex(0))),
                    CanonicalPoint(transform.OfPoint(triangle.get_Vertex(1))),
                    CanonicalPoint(transform.OfPoint(triangle.get_Vertex(2)))
                };
                Array.Sort(points, StringComparer.Ordinal);
                descriptors.Add(prefix + "-triangle|" + string.Join("|", points));
            }
        }

        private static void AppendCurve(
            Curve curve,
            Transform transform,
            ICollection<string> descriptors)
        {
            AppendPointSequence(
                "curve:" + curve.GetType().Name,
                curve.Tessellate(),
                transform,
                descriptors);
        }

        private static void AppendPointSequence(
            string prefix,
            IEnumerable<XYZ> points,
            Transform transform,
            ICollection<string> descriptors)
        {
            var forward = points
                .Select(point => CanonicalPoint(transform.OfPoint(point)))
                .ToArray();
            if (forward.Length == 0)
            {
                return;
            }

            var reverse = forward.Reverse().ToArray();
            var forwardText = string.Join("|", forward);
            var reverseText = string.Join("|", reverse);
            descriptors.Add(
                prefix + "|" + (StringComparer.Ordinal.Compare(forwardText, reverseText) <= 0
                    ? forwardText
                    : reverseText));
        }

        private static string CanonicalPoint(XYZ point)
        {
            return CanonicalGeometryFingerprint.Point(
                RevitUnitNormalizer.ToMillimeters(point.X),
                RevitUnitNormalizer.ToMillimeters(point.Y),
                RevitUnitNormalizer.ToMillimeters(point.Z));
        }
    }
}
