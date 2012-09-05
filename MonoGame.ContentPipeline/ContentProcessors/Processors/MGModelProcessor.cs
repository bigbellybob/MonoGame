using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Content.Pipeline;
using System.ComponentModel;
using MonoGameContentProcessors.Content;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;

namespace MonoGameContentProcessors.Processors
{
    [ContentProcessor(DisplayName = "MonoGame Model")]
    public class MGModelProcessor : ModelProcessor
    {
        protected override MaterialContent ConvertMaterial(MaterialContent material, ContentProcessorContext context)
        {
            return context.Convert<MaterialContent, MaterialContent>(material, "MGMaterialProcessor");
        }
    }
}
