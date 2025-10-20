using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jellies.src.pills;
internal enum PillEffect
{
    None,
    ClearRow,
    ClearColumn,
    ClearAdjacent,
    ClearColor,

    Regular,
    Dynamite,
    HorizontalClear,
    VerticalClear,
    ColorBomb
}

public record struct FreezeEffect(int Count);