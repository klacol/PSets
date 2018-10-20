using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PSet2YamlConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"PSet Manager started");
            Console.WriteLine($"A tool by the community of buildingSMART International");
            Console.WriteLine($"The home of PSet Manager is https://github.com/buildingsmart/bSDD");

            string sourceFolder = args[0];
            string targetFolder = args[1];
            Console.WriteLine($"Converting the PSets from this source folder: {sourceFolder}");
            Console.WriteLine($"Into YAML files in this target folder: {targetFolder}");

            Converter converter = new Converter(sourceFolder, targetFolder);

            Console.WriteLine($"Successfully finished - Be happy with your Open BIM");
        }
    }
}
