using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MDTadusMod.Data
{
    public class ItemDataEntry
    {
        [XmlAttribute("type")]
        public string Key { get; set; }

        [XmlText]
        public string Value { get; set; }
    }
}
