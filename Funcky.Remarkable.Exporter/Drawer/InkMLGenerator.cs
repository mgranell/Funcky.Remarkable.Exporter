using System;
using System.Collections.Generic;
using System.Text;

namespace Funcky.Remarkable.Exporter.Drawer
{
    public class InkMLGenerator
    {
        private readonly List<Page> pages;

        private readonly List<string> templates;

        private readonly int templateIndex;

        public InkMLGenerator(List<Page> pages, List<string> templates, int templateIndex)
        {
            this.pages = pages;
            this.templates = templates;
            this.templateIndex = templateIndex;
        }        
    }
}
