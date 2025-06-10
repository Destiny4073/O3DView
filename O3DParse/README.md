# O3DParse

[![Nuget](https://img.shields.io/nuget/v/o3dparse?logo=Nuget&logoColor=fff)](https://www.nuget.org/packages/O3DParse/)

Read and write OMSI model and configuration files. O3DParse provides extensible read and write support
for a number of OMSI file formats. These include:
 - O3D & RDY model files
 - CFG model files
 - OVH & BUS files
 - SCO scenery object files
 - CFG passenger cabin files
 - SLI spline definition files
 - CTI repaint files
 - MAP map tile files

O3DParse also includes a mesh-generator for splines.

## Usage

Loading an OMSI ini file (any of the cfg, bus, sco, etc... files) is as simple as:
```cs
using var fs = File.OpenRead(path);
var cfg = OmsiIniSerializer.DeserializeIniFile<OmsiCFGFile>(fs);
```

To reserialize back to a file, is equally straightforward:
```cs
using var fs = File.OpenWrite(path);
OmsiIniSerializer.SerializeIniFile(model, fs, leaveOpen:false, minifyCFG);
```
When `minifyCFG` is set to `false`, then all the original comments in the loaded file are re-exported, 
allowing for relatively lossless exports (some items may be re-ordered when exporting).

Using the O3D importer/exporter can be done as shown:
```cs
// Read the file
O3DFile o3d = O3DParser.ReadO3D(path);

// Write the file
WriteO3D(path, o3d);
```

## Extending the Library

The serializer can easily be extended to support more ini-like file formats. To add support for a new
format, simply define the data model in C# as a bunch of classes or structs, and annotate them with the
required O3DParse attributes. See the `OmsiCFGFile.cs` file for an example of how to annotate a data model.
