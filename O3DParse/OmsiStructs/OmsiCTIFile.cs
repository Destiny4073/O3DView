using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace O3DParse.Ini;

[Serializable]
public class OmsiCTIFile
{
    //[OmsiIniCommandFile] public string? path;

    public ItemCommand[]? items;

    [OmsiIniComments] public string? comments;
    [OmsiIniComments(true)] public string? postComments;
}
