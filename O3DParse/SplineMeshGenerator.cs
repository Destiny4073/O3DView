using O3DParse.Ini;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace O3DParse;

public static class SplineMeshGenerator
{
    /// <summary>
    /// Generates a mesh for the given spline.
    /// </summary>
    /// <param name="spline">The spline to generate a mesh for.</param>
    /// <param name="tessDist">The minimum distance between spline segments.</param>
    /// <param name="curveSag">The maximum amount of curve sag at any point on the tessellated curve. Set to -1 to disable sag based tessellation (faster)</param>
    /// <returns>The generate spline mesh.</returns>
    public static SimpleMesh GenerateSplineMesh(ISplineInstance spline, float tessDist, float curveSag)
    {
        List<O3DVert> verts = [];
        List<O3DTri> tris = [];

        if (spline.Length == 0)
            return new SimpleMesh() { vertices = [], triangles = [], materials = [] };

        IList<float> segSamples;
        if (curveSag > 0)
            segSamples = GenerateTessellationPoints(spline, tessDist, curveSag);
        else
            segSamples = GenerateTessellationPointsFast(spline, tessDist);

        int vCount = 0;
        foreach (var profilePart in spline.Profile.ProfileParts)
        {
            Vector3[] line = new Vector3[profilePart.points.Length];
            int vertsCreated = 0;
            foreach (var y in segSamples)
            {
                float skew = (spline.SkewStart * (1 - y / spline.Length) + spline.SkewEnd * y / spline.Length);
                if (spline.Mirror)
                    skew = -skew;

                for (int p = 0; p < line.Length; p++)
                    line[p] = new(profilePart.points[p].pos.X * spline.Width, skew * profilePart.points[p].pos.X, profilePart.points[p].pos.Y);

                if (spline.Mirror)
                    for (int p = 0; p < line.Length; p++)
                        line[p].X = -line[p].X;

                var (lPos, lRot) = EvaluateSpline(spline, new(0, y, 0));

                // To prevent there from being a gap between profile segments, we don't rotate the segments in the x
                // direction
                lRot.X = 0;
                // Transform the profile lines by the evaluated position along the spline
                //var rotQuat = Quaternion.CreateFromYawPitchRoll(lRot.X, lRot.Y, lRot.Z);
                //var mat = Matrix4x4.CreateWorld(lPos, Vector3.Transform(Vector3.UnitZ, rotQuat), Vector3.Transform(Vector3.UnitY, rotQuat));
                /*var (sa, ca) = MathF.SinCos(lRot.Y);
                var (sb, cb) = MathF.SinCos(lRot.X);
                var (sc, cc) = MathF.SinCos(lRot.Z);
                // XYZ ordered Tait-Bryan rotation matrix
                var mat = new Matrix4x4(
                    cb*cc, -cb*sc, cb*sc, 0,
                    ca*sc+cc*sa*sb, ca*cc-sa*sb*sc, -cb*sa, 0,
                    sa*sc-ca*cc*sb, cc*sa+ca*sb*sc, ca*cc-sa*sb*sc, 0,
                    lPos.X, lPos.Y, lPos.Z, 1
                );*/
                var (sa, ca) = MathF.SinCos(lRot.X);
                var (sb, cb) = MathF.SinCos(lRot.Y);
                var (sc, cc) = MathF.SinCos(lRot.Z);
                /*// XZX ordered Euler rotation matrix
                var mat = new Matrix4x4(
                    cb, -cc*sb, sb*sc, 0,
                    ca*sb, ca*cb*cc-sa*sc, -cc*sa-ca*cb*sc, 0,
                    sa*sb, ca*sc+cb*cc*sa, ca*cc-cb*sa*sc, 0,
                    lPos.X, lPos.Y, lPos.Z, 1
                );*/
                /*// XYX ordered Euler rotation matrix
                var mat = new Matrix4x4(
                    cb, sb * sc, cc* sb, 0,
                    sa * sb, ca * cc * cc - cb * sa * sc, -ca * sc - cb * cc * sa, 0,
                    -ca * sb, cc * sa + ca * cb * sc, ca * cb * cc - sa * sc, 0,
                    lPos.X, lPos.Y, lPos.Z, 1
                );*/
                // YZY ordered Euler rotation matrix
                var mat = new Matrix4x4(
                    ca*cb*cc-sa*sc, -ca*sb, cc*sa+ca*cb*sc, 0,
                    cc*sb, cb, sb*sc, 0,
                    -ca*sc-cb*cc*sa, sa*sb, ca*cc-cb*sa*sc, 0,
                    lPos.X, lPos.Y, lPos.Z, 1
                );
                for (int i = 0; i < line.Length; i++)
                {
                    var v = Vector3.Transform(line[i], mat);
                    var pnt = profilePart.points[i];
                    var uv = pnt.texcoord;
                    uv.Y *= y + skew * pnt.pos.X;

                    verts.Add(new()
                    {
                        pos = v,
                        normal = new O3DVec3(0, 1, 0),
                        uv = uv
                    });
                    vertsCreated++;
                }
            }

            // Construct triangles for the current strip (ie the current [profile])
            // The vertex array should look like this for a [profile] with three points:
            //   {End}
            // 9--10--11      6---7
            // | / | / |      | / |
            // 6---7---8      4---5
            // | / | / |      | / |
            // 3---4---5      2---3
            // | / | / |      | / |
            // 0---1---2      0---1
            //  {Start}       /\ For a profile with 2 points
            int profileLen = line.Length;
            ushort texIndex = unchecked((ushort)profilePart.textureIndex);
            if (!spline.Mirror)
            {
                for (int x = 0; x < segSamples.Count - 1; x++)
                {
                    for (int y = 0; y < profileLen - 1; y++)
                    {
                        // CW winding
                        unchecked
                        {
                            tris.Add(new((uint)(y + vCount + x * profileLen),           // 0 / 0
                                         (uint)(y + vCount + (x + 1) * profileLen + 1), // 4 / 3
                                         (uint)(y + vCount + x * profileLen + 1),       // 1 / 1
                                         texIndex));
                            tris.Add(new((uint)(y + vCount + x * profileLen),           // 0 / 0
                                         (uint)(y + vCount + (x + 1) * profileLen),     // 3 / 2
                                         (uint)(y + vCount + (x + 1) * profileLen + 1), // 4 / 3
                                         texIndex));
                        }
                    }
                }
            } 
            else
            {
                for (int x = 0; x < segSamples.Count - 1; x++)
                {
                    for (int y = 0; y < profileLen - 1; y++)
                    {
                        // CCW winding
                        unchecked
                        {
                            tris.Add(new((uint)(y + vCount + x * profileLen + 1),       // 1 / 1
                                         (uint)(y + vCount + (x + 1) * profileLen + 1), // 4 / 3
                                         (uint)(y + vCount + x * profileLen),           // 0 / 0
                                         texIndex));
                            tris.Add(new((uint)(y + vCount + (x + 1) * profileLen + 1), // 4 / 3
                                         (uint)(y + vCount + (x + 1) * profileLen),     // 3 / 2
                                         (uint)(y + vCount + x * profileLen),           // 0 / 0
                                         texIndex));
                        }
                    }
                }
            }

            vCount += vertsCreated;
        }

        return new SimpleMesh() { vertices = [.. verts], triangles = [.. tris], materials = [] };
    }

    /// <summary>
    /// Computes a position along a spline given offset coordinates.
    /// </summary>
    /// <param name="spline">The spline to evaluate.</param>
    /// <param name="offset">The position offset along the spline. X is across the width of the 
    /// spline; Y is forward along the spline; Z is up relative to the spline.</param>
    /// <returns>(The computed position, the computed rotation)</returns>
    public static (Vector3 pos, Vector3 rot) EvaluateSpline(ISplineInstance spline, Vector3 offset)
    {
        // Split the split evaluation into separate gradient and radius steps
        // Gradient
        // Evaluate: f_z(y, x) = y^3*c + y^2*a + y*b + x*(y*cant_delta + cant_0) {0 <= y <= length}
        // The rotation is given by:
        // f_rx(y, x) = atan(3y^2*c + 2y*a + b + x*cant_delta) {0 <= y <= length}
        var (ox, oy, oz) = (offset.X, offset.Y, offset.Z);
        float cantDelta = (spline.CantEnd - spline.CantStart) / 100 / spline.Length;
        float cant = oy * cantDelta + spline.CantStart / 100;
        var coeffs = ComputeSplineGradientCoeffs(spline);
        var (a, b, c) = (coeffs.X, coeffs.Y, coeffs.Z);
        float oy2 = oy * oy;
        float pz = oy * oy2 * c + oy2 * a + oy * b;
        float rx = MathF.Atan(3 * oy2 * c + 2 * oy * a + b + ox * cant);

        pz += oz;

        // Cant
        float ry = MathF.Atan(cant);
        // TODO: The x coordinate should be clamped to the width of the spline
        pz += -ox * cant;

        // Radius
        float px = ox, py = oy, rz = 0;
        if (MathF.Abs(spline.Radius) > 0)
        {
            rz = oy / spline.Radius;
            float k = ox - spline.Radius;
            var (sr, cr) = MathF.SinCos(rz);
            px = k * cr + spline.Radius;
            py = -k * sr;
            rz = -rz;
        }

        var pos = new Vector3(px, pz, py);
        var rot = new Vector3(rx, ry, rz);
        // World space
        /*if apply_rot or world_space:
            if bpy.app.version < (2, 80):
                pos = pos * mathutils.Matrix.Rotation(math.radians(self.rot), 4, "Z")
            else:
                pos = pos @ mathutils.Matrix.Rotation(math.radians(self.rot), 4, "Z")
            rot.z += math.radians(-self.rot)

        if world_space:
            pos += Vector(self.pos).xzy*/

        return (pos, rot);
    }

    /// <summary>
    /// Computes the hermite spline coefficients for this spline from the gradient and delta height properties.
    /// </summary>
    /// <param name="spline"></param>
    /// <returns></returns>
    private static Vector3 ComputeSplineGradientCoeffs(ISplineInstance spline)
    {
        Vector3 res = default;
        res.Y = spline.StartGrad / 100;
        if (spline.UseDeltaHeight)
        {
            float grad = (spline.EndGrad - spline.StartGrad) / 100 * spline.Length;
            float x = spline.DeltaHeight - spline.StartGrad / 100 * spline.Length;
            float length2 = spline.Length * spline.Length;

            res.Z = (grad - 2 * x) / (length2 * spline.Length);
            res.X = (-grad + 3 * x) / length2;
        }
        else
        {
            res.Z = 0;
            res.X = (spline.EndGrad - spline.StartGrad) / 100 / (2 * spline.Length);
        }
        return res;
    }

    /// <summary>
    /// Generates a set of z coordinates along the spline which represents an "ideal" sampling of the spline.
    /// </summary>
    /// <remarks>
    /// This version of the method only considers the tessellation distance, but is much faster to compute.
    /// </remarks>
    /// <param name="spline"></param>
    /// <param name="tessDist"></param>
    /// <returns></returns>
    private static IList<float> GenerateTessellationPointsFast(ISplineInstance spline, float tessDist)
    {
        float radius = MathF.Abs(spline.Radius);
        if (radius > 0)
        {
            float revs = MathF.Min(spline.Length / radius, MathF.PI * 2);
            // Arc distance based angle increment
            float dr = tessDist / radius;
            int nSegments = (int)Math.Max(MathF.Ceiling(revs / dr), 1);
            dr = revs / nSegments;

            float[] samples = new float[nSegments];
            for (int i = 0; i <= nSegments; i++)
                samples[i] = i * dr * radius;
            return samples;
        }
        else
        {
            // Otherwise add samples for the constant tessellation factor
            // Note that the arc segment already includes the constant tessellation factor
            int nSegments = (int)Math.Max(MathF.Ceiling(spline.Length / tessDist), 1);
            float dx = spline.Length / nSegments;

            float[] samples = new float[nSegments];
            for (int i = 0; i <= nSegments; i++)
                samples[i] = i * dx;
            return samples;
        }
    }

    /// <summary>
    /// Generates a set of z coordinates along the spline which represents an "ideal" sampling of the spline.
    /// </summary>
    /// <remarks>
    /// This is a non-trivial problem to solve, and hence the answer is somewhat approximate here. Our spline is 
    /// essentially a hermite curve along the y axis and an arc along the xz plane.For ease of implementation we
    /// consider the sampling of these separately and then merge and weld the samplings together.
    ///
    /// The Y axis can be described by the following function: 
    /// (see https://www.desmos.com/calculator/7xfjwmqtpz for my calculator)
    ///         f_y(z, x) = z^3*c + z^2*a + z* b + x* (z* cant_delta + cant_0) {0 <= z <= length}
    /// To approximate the curvature of the spline we take the arctangent of the first derivative(essentially
    /// converting the gradient of the function into an angle) :
    ///         f_dy(z, x) = 3z^2* c + 2z* a + b + x* cant_delta {0 <= z <= length}
    ///         f_curvature(z, x) = abs(atan(f_dy(z, x))) * (1 + 1/r)
    /// We want to sample this at regular horizontal slices of the curvature equation (ie the higher the gradient of
    /// the curvature equation the more samples to generate), we do this by taking the inverse of the curvature equation
    /// and sampling it at regular intervals along the x axis. This is represented by the following expression:
    ///         f_icurvature(z, x) = (-a +- sqrt(a^2 - 3c(b + x*cant_delta) +- 3c*tan(z)) / 3c { 0 <= z <= pi / 2}
    /// Because of the two +- operations in the above equation, we can actually get up to four points per iteration.
    /// </remarks>
    /// <param name="spline">The spline to sample.</param>
    /// <param name="tessDist">The minimum distance between points.</param>
    /// <param name="curveSag">The maximum amount of curve sag at any point on the tessellated curve.</param>
    /// <returns>A list of optimal spline points to sample.</returns>
    private static IList<float> GenerateTessellationPoints(ISplineInstance spline, float tessDist, float curveSag)
    {
        List<float> samples = new(Math.Max((int)(spline.Length / Math.Max(tessDist, 0.05f)), 0));
        int nGradSamples = 0;
        // Add samples to represent gradient curvature if needed
        if (spline.StartGrad != spline.EndGrad || spline.UseDeltaHeight)
        {
            var coeffs = ComputeSplineGradientCoeffs(spline);
            var (a, b, c) = (coeffs.X, coeffs.Y, coeffs.Z);
            if (a != 0 || c != 0)
            {
                float cantDelta = (spline.CantEnd - spline.CantStart) / spline.Length / 100;
                // Maybe we should consider the value of x?
                float x = 0;
                float _3c = c * 3;
                float _2a = a * 2;
                float a23cbxc = 0;
                float bxc2a = 0;
                if (spline.UseDeltaHeight)
                    a23cbxc = a * a - _3c * (b + x * cantDelta);
                else
                    bxc2a = (-b - x * cantDelta) / _2a;
                float z = 0;
                float halfPi = MathF.PI / 2;

                // Check this wouldn't make too many samples...
                if (halfPi / curveSag > 10000)
                {
                    //log("[ERROR] Spline tesselation would take too long, please increase the spline curve sag distance!")
                    // Return a basic tesselation...
                    int nSegments = Math.Min(Math.Max((int)MathF.Ceiling(spline.Length / tessDist), 1), 100);
                    float dx = spline.Length / nSegments;
                    return Enumerable.Range(0, nSegments + 1).Select(x => x * dx).ToArray();
                }

                if (spline.UseDeltaHeight)
                {
                    while (z <= halfPi)
                    {
                        float _3ctanz = _3c * MathF.Tan(z);
                        if (a23cbxc >= -_3ctanz)
                        {
                            float sqrt = MathF.Sqrt(a23cbxc + _3ctanz);
                            float ic00 = (-a + sqrt) / _3c;
                            float ic01 = (-a - sqrt) / _3c;
                            if (ic00 >= 0 && ic00 <= spline.Length)
                                samples.Add(ic00);
                            if (ic01 >= 0 && ic01 <= spline.Length)
                                samples.Add(ic01);
                        }
                        if (a23cbxc >= _3ctanz)
                        {
                            float sqrt = MathF.Sqrt(a23cbxc - _3ctanz);
                            float ic10 = (-a + sqrt) / _3c;
                            float ic11 = (-a - sqrt) / _3c;
                            if (ic10 >= 0 && ic10 <= spline.Length)
                                samples.Add(ic10);
                            if (ic11 >= 0 && ic11 <= spline.Length)
                                samples.Add(ic11);
                        }

                        z += curveSag * 10;
                    }
                }
                else
                {
                    while (z <= halfPi)
                    {
                        // f_icurvature2(z, x) = (-b -x*c_d)/(2a) +- (tan z)/(2a)
                        float t2a = MathF.Tan(z) / _2a;
                        float ic20 = bxc2a + t2a;
                        float ic21 = bxc2a - t2a;

                        if (ic20 >= 0 && ic20 <= spline.Length)
                            samples.Add(ic20);
                        if (ic21 >= 0 && ic21 <= spline.Length)
                            samples.Add(ic21);

                        z += curveSag * 10;
                    }
                }
            }

            nGradSamples = samples.Count;
        }

        // Add samples to represent to curve radius of the spline if needed
        float radius = MathF.Abs(spline.Radius);
        float dr = float.PositiveInfinity;
        if (radius > 0)
        {
            float revs = MathF.Min(spline.Length / radius, MathF.PI * 2);
            // Arc distance based angle increment
            float dr_a = tessDist / radius;
            // Sag distance based angle increment, clamped to 0.06deg < x < 90deg
            float dr_b = 2 * MathF.Acos(1 - Math.Clamp(curveSag / radius, 0.001f, 0.294f));
            dr = MathF.Min(dr_a, dr_b);
            int nSegments = (int)Math.Max(MathF.Ceiling(revs / dr), 1);
            dr = revs / nSegments;
            for (int i = 0; i <= nSegments; i++)
                samples.Add(i * dr * radius);
        }
        else
        {
            // Otherwise add samples for the constant tessellation factor
            // Note that the arc segment already includes the constant tessellation factor
            int nSegments = (int)Math.Max(MathF.Ceiling(spline.Length / tessDist), 1);
            float dx = spline.Length / nSegments;
            for (int i = 0; i <= nSegments; i++)
                samples.Add(i * dx);
        }

        // Now weld the samples together based on a heuristic
        if (samples.Count > 1)
        {
            samples.Sort();

            // This tuple stores the sample position, and it's weight (used for averaging); when a sample is consumed its
            // weight is set to 0 and the sample it's merged with has its weight incremented
            (float x, int n)[] samplesWeighted = new (float x, int n)[samples.Count];
            for (int i = 0; i < samplesWeighted.Length; i++)
                samplesWeighted[i] = (samples[i], 1);
            // log(f"spline-{self.id}\tmerge_dist: grad={(self.length/max(float(n_grad_samples), 0.1)/3):3f} "
            //     f"const={spline_tess_dist:3f} arc={dr*max(radius, 0.01)/2:3f} length={self.length*0.9:3f}")
            float mergeDist = Math.Min(Math.Min(Math.Min(
                              spline.Length / Math.Max(nGradSamples, 0.1f) / 3,
                              tessDist),
                              dr * Math.Max(radius, 0.01f) / 2),
                              spline.Length * 0.9f);

            int last = 0;
            for (int i = 1; i < samplesWeighted.Length - 1; i++)
            {
                float d = samplesWeighted[i].x - samplesWeighted[last].x;
                if (d < mergeDist)
                {
                    var (x, n) = samplesWeighted[last];
                    samplesWeighted[last] = ((x * n + samplesWeighted[i].x) / (n + 1), n + 1);
                    samplesWeighted[i] = (0, 0);
                }
                else
                {
                    last = i;
                }
            }
            samples = samplesWeighted.Where(x => x.n > 0).Select(x => x.x).ToList();
            // Make sure the start and end points are fixed
            samples[0] = 0;
            samples[^1] = spline.Length;
        }

        return samples;
    }
}

public interface ISplineInstance
{
    public abstract ISplineProfiles Profile { get; }
    public abstract float Length { get; }
    public abstract float Radius { get; }
    public abstract float StartGrad { get; }
    public abstract float EndGrad { get; }
    public abstract float CantStart { get; }
    public abstract float CantEnd { get; }
    public abstract float SkewStart { get; }
    public abstract float SkewEnd { get; }
    public abstract bool UseDeltaHeight { get; }
    public abstract float DeltaHeight { get; }
    public abstract bool Mirror { get; }
    public abstract float Width { get; }
}

public interface ISplineProfiles
{
    public abstract IEnumerable<ProfileCommand> ProfileParts { get; }
}

public struct SimpleMesh
{
    public O3DVert[] vertices;
    public O3DTri[] triangles;
    public O3DMaterial[] materials;
}
