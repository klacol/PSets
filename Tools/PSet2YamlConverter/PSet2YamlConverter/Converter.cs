using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using PSD_IFC5;
using Newtonsoft.Json;

namespace PSet2YamlConverter
{
    class Converter
    {
        List<string> StandardLanguages = new List<string>
            {
                "en-EN",
                "es-ES",
                "de-DE",
                "fr-FR",
                "ja-JP",
                "ru-RU"
            };
 
        public Converter(string sourceFolderXml,string targetFolderYaml, string targetFolderJson)
        {
            string propertySetVersionList = string.Empty;
            string propertySetTemplateList = string.Empty;
            string propertyTypeList = string.Empty;
            string propertyUnitList = string.Empty;

            foreach (string sourceFile in Directory.EnumerateFiles(sourceFolderXml, "PSet*.xml").OrderBy(x => x).ToList())//.Where(x=>x.Contains("Pset_SpaceThermalRequirements")))
            {
                Console.WriteLine($"Opening {sourceFile.Replace(sourceFolderXml + @"\", string.Empty)}");
                PropertySetDef pSet = PropertySetDef.LoadFromFile(sourceFile);

                if (!propertySetVersionList.Contains(pSet.IfcVersion.version))
                    propertySetVersionList += pSet.IfcVersion.version + ",";
                if (!propertySetTemplateList.Contains(pSet.templatetype.ToString()))
                    propertySetTemplateList += pSet.templatetype.ToString() + ",";

                PropertySet propertySet = new PropertySet()
                {
                    name = pSet.Name,
                    ifdGuid = "",
                    ifcVersion = new IfcVersion()
                    {
                        version = ConvertToSematicVersion(pSet.IfcVersion.version).ToString(),
                        schema = pSet.IfcVersion.schema
                    },
                    definition = pSet.Definition
                };

                propertySet.applicableIfcClasses = new List<ApplicableIfcClass>();
                foreach (var applicableClass in pSet.ApplicableClasses)
                {
                    propertySet.applicableIfcClasses.Add(new ApplicableIfcClass()
                    {
                        name = applicableClass,
                        type = pSet.ApplicableTypeValue
                    });
                }

                //Insert missing standard localizations as dummys
                propertySet.localizations = new List<Localization>();
                foreach (string standardLanguage in StandardLanguages.OrderBy(x=>x))
                    if (propertySet.localizations.Where(x => x.language == standardLanguage).FirstOrDefault() == null)
                        propertySet.localizations.Add(new Localization()
                        {
                            language = standardLanguage,
                            name = string.Empty,
                            definition = string.Empty
                        });

                propertySet.properties = LoadProperties(pSet.PropertyDefs);
                propertySet = Utils.PrepareTexts(propertySet);
                string targetFileYaml = sourceFile.Replace("xml", "YAML").Replace(sourceFolderXml, targetFolderYaml);
                string targetFileJson = sourceFile.Replace("xml", "JSON").Replace(sourceFolderXml, targetFolderJson);
                Console.WriteLine($"   Writing {targetFileYaml.Replace(targetFolderYaml + @"\", string.Empty)}");
                Console.WriteLine($"   Writing {targetFileJson.Replace(targetFolderJson + @"\", string.Empty)}");

                var ScalarStyleSingleQuoted = new YamlMemberAttribute()
                {
                    ScalarStyle = ScalarStyle.SingleQuoted
                };

                var yamlSerializer = new SerializerBuilder()
                    //.WithNamingConvention(new CamelCaseNamingConvention())
                    .WithAttributeOverride<PropertySet>(nc => nc.name, ScalarStyleSingleQuoted)
                    .WithAttributeOverride<PropertySet>(nc => nc.definition, ScalarStyleSingleQuoted)
                    .WithAttributeOverride<Localization>(nc => nc.name, ScalarStyleSingleQuoted)
                    .WithAttributeOverride<Localization>(nc => nc.definition, ScalarStyleSingleQuoted)
                    .Build();

                string yamlContent = yamlSerializer.Serialize(propertySet);
                File.WriteAllText(targetFileYaml, yamlContent, Encoding.UTF8);
                Console.WriteLine("   The PSet was saved as YAML file");

                JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings();
                jsonSerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
                jsonSerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                string jsonContent = JsonConvert.SerializeObject(propertySet,Formatting.Indented, jsonSerializerSettings);
                File.WriteAllText(targetFileJson, jsonContent, Encoding.UTF8);
                Console.WriteLine("   The PSet was saved as JSON file");

                var yamlDeserializer = new DeserializerBuilder()
                    .Build();
                try
                {
                    propertySet = yamlDeserializer.Deserialize<PropertySet>(new StringReader(File.ReadAllText(targetFileYaml)));
                    Console.WriteLine("   The YAML file is valid");
                }
                catch (Exception ex)
                {
                    Console.Write("   ERROR!");
                    Console.WriteLine(ex.Message);
                }
            }
            Console.WriteLine($"Used Versions in PSets: {propertySetVersionList}");
            Console.WriteLine($"Used Templates in PSets: {propertySetTemplateList}");
            Console.WriteLine($"Used PropertyTypes: {propertyTypeList}");
            Console.WriteLine($"Used Units: {propertyUnitList}");
        }

        private List<Property> LoadProperties(List<PropertyDef> PropertyDefs)
        {
            List<Property> properties = new List<Property>();
            foreach (PropertyDef psetProperty in PropertyDefs)
            {
                //
                int itemNumber = 0;
                foreach (var item in psetProperty.Items)
                {
                    string ty = item.ToString();
                    if (item.ToString().Contains("PropertyType"))
                        break;
                    itemNumber++;
                }
                PropertyType psetValueType = (PropertyType)psetProperty.Items[itemNumber];
                PropertyTypeTypePropertyBoundedValue psetBoundedValue = null;
                PropertyTypeTypeComplexProperty psetComplexProperty = null;
                PropertyTypeTypePropertyEnumeratedValue psetEnumeratedValue = null;
                PropertyTypeTypePropertyListValue psetListValue = null;
                PropertyTypeTypePropertyReferenceValue psetReferenceValue = null;
                PropertyTypeTypePropertySingleValue psetSingleValue = null;
                PropertyTypeTypePropertyTableValue psetTableValue = null;

                string valueTypeAsString = psetValueType.Item.ToString().Replace("PSet2YamlConverter.", "");

                Property property = new Property()
                {
                    name = psetProperty.Items[0].ToString(),
                    ifdGuid = Utils.GuidConverterToIfcGuid(psetProperty.ifdguid),
                    definition = psetProperty.Items[2].ToString()
                };

                property.localizations = new List<Localization>();
                PropertyDefNameAliases nameAliases;
                if (psetProperty.Items.Length >= 4)
                {
                    nameAliases = (PropertyDefNameAliases)psetProperty.Items[3];
                    PropertyDefDefinitionAliases definitionAliases = new PropertyDefDefinitionAliases();
                    if (psetProperty.Items.Length >= 5)
                        definitionAliases = (PropertyDefDefinitionAliases)psetProperty.Items[4];

                    foreach (var alias in nameAliases.NameAlias)
                    {
                        property.localizations.Add(new Localization()
                        {
                            language = alias.lang,
                            name = alias.Value ?? string.Empty,
                            definition = definitionAliases.DefinitionAlias.Where(x => x.lang == alias.lang)?.FirstOrDefault()?.Value ?? string.Empty
                        });
                    }
                }

                //Insert missing standard localizations as dummys
                foreach (string standardLanguage in StandardLanguages.OrderBy(x=>x))
                    if (property.localizations.Where(x => x.language == standardLanguage).FirstOrDefault() == null)
                        property.localizations.Add(new Localization()
                        {
                            language = standardLanguage,
                            name = string.Empty,
                            definition = string.Empty
                        });

                IfcDataType resultParseIfcDataType;
                ReferenceClass resultParseReferenceClass;

                switch (valueTypeAsString)
                {
                    case "PropertyTypeTypePropertyBoundedValue":
                        psetBoundedValue = (PropertyTypeTypePropertyBoundedValue)psetValueType.Item;
                        property.typePropertyBoundedValue = new TypePropertyBoundedValue();
                        property.typePropertyBoundedValue.typeName = nameof(TypePropertyBoundedValue);
                        Enum.TryParse(psetSingleValue?.DataType?.type.ToString() ?? string.Empty, out resultParseIfcDataType);
                        property.typePropertyBoundedValue.dataType = resultParseIfcDataType;
                        property.typePropertyBoundedValue.unitType = psetBoundedValue.UnitType.ToString() ?? string.Empty;
                        property.typePropertyBoundedValue.LowerBoundValue = psetBoundedValue.ValueRangeDef.LowerBoundValue.value?.ToString() ?? string.Empty;
                        property.typePropertyBoundedValue.UpperBoundValue = psetBoundedValue.ValueRangeDef.UpperBoundValue.value?.ToString() ?? string.Empty;
                        break;
                    case "PropertyTypeTypeComplexProperty":
                        psetComplexProperty = (PropertyTypeTypeComplexProperty)psetValueType.Item;
                        property.typeComplexProperty = new TypeComplexProperty();
                        property.typeComplexProperty.name = psetComplexProperty.name ?? string.Empty;
                        property.typeComplexProperty.subProperties = LoadProperties(psetComplexProperty.PropertyDef); //Recursiv loading
                        break;
                    case "PropertyTypeTypePropertyEnumeratedValue":
                        psetEnumeratedValue = (PropertyTypeTypePropertyEnumeratedValue)psetValueType.Item;
                        property.typePropertyEnumeratedValue = new TypePropertyEnumeratedValue();
                        property.typePropertyEnumeratedValue.listName = psetEnumeratedValue.EnumList.name;
                        property.typePropertyEnumeratedValue.enumerationValues = new List<EnumerationValue>();
                        foreach (var enumValue in psetEnumeratedValue.EnumList.EnumItem)
                        {
                            EnumerationValue enumerationValue = new EnumerationValue()
                            {
                                ifdGuid = string.Empty,// Utils.GuidConverterToIfcGuid(Guid.NewGuid().ToString()),
                                name = Utils.CleanUp(enumValue),
                                definition = string.Empty,
                                localizations = new List<Localization>()
                            };

                            foreach (string standardLanguage in StandardLanguages.OrderBy(x=>x))
                            {
                                enumerationValue.localizations.Add(new Localization()
                                {
                                    language = standardLanguage,
                                    name = Utils.FirstUpperRestLower(enumValue.ToString()),
                                    definition = string.Empty
                                });
                            }

                            property.typePropertyEnumeratedValue.enumerationValues.Add(enumerationValue);
                        }
                        break;
                    case "PropertyTypeTypePropertyListValue":
                        psetListValue = (PropertyTypeTypePropertyListValue)psetValueType.Item;
                        property.typePropertyListValue = new TypePropertyListValue();
                        Enum.TryParse(psetListValue?.ListValue.DataType?.type.ToString() ?? string.Empty, out resultParseIfcDataType);
                        property.typePropertyListValue.dataType = resultParseIfcDataType;
                        property.typePropertyListValue.unitType = psetListValue?.ListValue.UnitType?.type.ToString() ?? string.Empty;
                        property.typePropertyListValue.measureType = "";
                        if (psetListValue.ListValue.Values.ValueItem.Count>0)
                        { 
                            property.typePropertyListValue.listValues = new List<string>();
                            foreach (string valueItem in psetListValue.ListValue.Values.ValueItem)
                                property.typePropertyListValue.listValues.Add(valueItem);
                        }
                        break;
                    case "PropertyTypeTypePropertyReferenceValue":
                        psetReferenceValue = (PropertyTypeTypePropertyReferenceValue)psetValueType.Item;
                        property.typePropertyReferenceValue = new TypePropertyReferenceValue();

                        Enum.TryParse(psetReferenceValue.reftype.ToString() ?? string.Empty, out resultParseReferenceClass);
                        property.typePropertyReferenceValue.refType = resultParseReferenceClass;
                        if (property.typePropertyReferenceValue.refType.ToString().StartsWith("Ifc"))
                        {
                            property.typePropertyReferenceValue.guid = string.Empty;
                            property.typePropertyReferenceValue.url = "http://buildingsmart-tech.org";
                            property.typePropertyReferenceValue.libraryName = "Industry Foundation Classes by buildingSMART International";
                            property.typePropertyReferenceValue.sectionref = "Core Schema";
                        }
                        else
                        {
                            property.typePropertyReferenceValue.guid = string.Empty;
                            property.typePropertyReferenceValue.url = string.Empty;
                            property.typePropertyReferenceValue.libraryName = string.Empty;
                            property.typePropertyReferenceValue.sectionref = string.Empty;
                        }
                        break;
                    case "PropertyTypeTypePropertySingleValue":
                        psetSingleValue = (PropertyTypeTypePropertySingleValue)psetValueType.Item;
                        property.typePropertySingleValue = new TypePropertySingleValue();
                        Enum.TryParse(psetSingleValue?.DataType?.type.ToString() ?? string.Empty, out resultParseIfcDataType);
                        property.typePropertySingleValue.dataType = resultParseIfcDataType;
                        property.typePropertySingleValue.unitType = psetSingleValue?.UnitType?.type.ToString() ?? string.Empty;
                        property.typePropertySingleValue.measureType = "";
                        break;
                    case "PropertyTypeTypePropertyTableValue":
                        psetTableValue = (PropertyTypeTypePropertyTableValue)psetValueType.Item;
                        property.typePropertyTableValue = new TypePropertyTableValue();
                        property.typePropertyTableValue.Expression = psetTableValue.Expression;

                        property.typePropertyTableValue.DefiningValue = new TableDefValues();
                        Enum.TryParse(psetTableValue?.DefiningValue?.DataType?.type.ToString() ?? string.Empty, out resultParseIfcDataType);
                        property.typePropertyTableValue.DefiningValue.dataType = resultParseIfcDataType;
                        property.typePropertyTableValue.DefiningValue.unitType = psetTableValue?.DefiningValue.UnitType?.type.ToString() ?? string.Empty;
                        property.typePropertyTableValue.DefiningValue.measureType = "";

                        property.typePropertyTableValue.DefinedValue = new TableDefValues();
                        Enum.TryParse(psetTableValue?.DefinedValue.DataType?.type.ToString() ?? string.Empty, out resultParseIfcDataType);
                        property.typePropertyTableValue.DefinedValue.dataType = resultParseIfcDataType;
                        property.typePropertyTableValue.DefinedValue.unitType = psetTableValue?.DefiningValue.UnitType?.type.ToString() ?? string.Empty;
                        property.typePropertyTableValue.DefinedValue.measureType = "";
                        break;
                };

                properties.Add(property);
            }

            return properties;
        }

        private System.Version ConvertToSematicVersion(string classicIfcVersion)
        {
            System.Version version = new System.Version(0,0,0,0);

            switch (classicIfcVersion)
            { 
                case "IFC 2x3":
                    version = new System.Version(2, 3, 0, 0);
                    break;
                case "IFC4":
                    version = new System.Version(4, 0, 0, 0);
                    break;
            }

            return version;
        }

    }
}
