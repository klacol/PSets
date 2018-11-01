﻿using System;
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
using System.Net;
using System.Xml.Serialization;
using bsDD.NET;
using bsDD.NET.Model.Objects;

namespace PSet2YamlConverter
{
    class Converter
    {
        private List<string> StandardLanguages = new List<string>
            {
                "en-EN",
                "es-ES",
                "de-DE",
                "fr-FR",
                "ja-JP",
                "ru-RU"
            };

        private bool CheckBSDD = false;
        private Bsdd _bsdd;  

        public Converter(string sourceFolderXml,string targetFolderYaml, string targetFolderJson, bool checkBSDD = false)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            CheckBSDD = checkBSDD;
            _bsdd = new Bsdd();

            string propertySetVersionList = string.Empty;
            string propertySetTemplateList = string.Empty;
            string propertyTypeList = string.Empty;
            string propertyUnitList = string.Empty;

            foreach (string sourceFile in Directory.EnumerateFiles(sourceFolderXml, "PSet*.xml").OrderBy(x => x).ToList())//.Where(x=>x.Contains("Pset_ConstructionResource")))
            {

                PropertySetDef pSet = PropertySetDef.LoadFromFile(sourceFile);
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine($"Checking PSet {pSet.Name}");
                Console.WriteLine($"Opened PSet-File {sourceFile.Replace(sourceFolderXml + @"\", string.Empty)}");
                IfdConceptList ifdConceptList = _bsdd.SearchNests(pSet.Name);
                if (ifdConceptList == null)
                {
                    Console.WriteLine($"Could not find the PSet in bSDD");
                }
                else
                {
                    IfdConcept bsddPSet = ifdConceptList.IfdConcept.FirstOrDefault();
                    Console.WriteLine($"Loaded PSet from bSDD");
                    Console.WriteLine($"Guid:        {bsddPSet.Guid}");
                    Console.WriteLine($"Status:      {bsddPSet.Status}");
                    Console.WriteLine($"VersionDate: {bsddPSet.VersionDate}");
                }

                    
                if (!propertySetVersionList.Contains(pSet.IfcVersion.version))
                    propertySetVersionList += pSet.IfcVersion.version + ",";
                if (!propertySetTemplateList.Contains(pSet.templatetype.ToString()))
                    propertySetTemplateList += pSet.templatetype.ToString() + ",";

                PropertySet propertySet = new PropertySet()
                {
                    name = pSet.Name,
                    ifdGuid = "",
                    legacyGuid = "",
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

                Console.WriteLine($"Now checking the properties within the PSet");
                propertySet.properties = LoadProperties(pSet, pSet.PropertyDefs);
                propertySet = Utils.PrepareTexts(propertySet);

                string targetFileYaml = sourceFile.Replace("xml", "YAML").Replace(sourceFolderXml, targetFolderYaml);
                string targetFileJson = sourceFile.Replace("xml", "json").Replace(sourceFolderXml, targetFolderJson);
                //Console.WriteLine($"   Writing {targetFileYaml.Replace(targetFolderYaml + @"\", string.Empty)}");
                //Console.WriteLine($"   Writing {targetFileJson.Replace(targetFolderJson + @"\", string.Empty)}");

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
                Console.WriteLine("The PSet was saved as YAML file");

                JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings();
                jsonSerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
                jsonSerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                string jsonContent = JsonConvert.SerializeObject(propertySet,Formatting.Indented, jsonSerializerSettings);
                File.WriteAllText(targetFileJson, jsonContent, Encoding.UTF8);
                Console.WriteLine("The PSet was saved as JSON file");

                var yamlDeserializer = new DeserializerBuilder()
                    .Build();
                try
                {
                    propertySet = yamlDeserializer.Deserialize<PropertySet>(new StringReader(File.ReadAllText(targetFileYaml)));
                    Console.WriteLine("The YAML file is valid");
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

        private List<Property> LoadProperties(PropertySetDef pset, List<PropertyDef> PropertyDefs)
        {
            List<Property> properties = new List<Property>();
            foreach (PropertyDef psetProperty in PropertyDefs)
            {
                Console.WriteLine("      .................");
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
                    dictionaryIdentifier = "http://bsdd.buildingsmart.org/IFC",
                    ifdGuid = "",
                    legacyGuid = psetProperty.ifdguid,
                    legacyGuidAsIfcGlobalId = Utils.GuidConverterToIfcGuid(psetProperty.ifdguid),
                    definition = psetProperty.Items[2].ToString()
                };
                Console.WriteLine($"      Name: {property.name}");
                if (CheckBSDD)
                if (property.legacyGuidAsIfcGlobalId.Length == 0)
                { 
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"      ERROR: The GUID for {property.name} is missing in PSet!");
                    Console.ResetColor();
                }
                if (CheckGuidWithBsdd(property.legacyGuidAsIfcGlobalId) == false)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"      ERROR: The GUID {property.legacyGuidAsIfcGlobalId} for {property.name} is not resolved by http://bsdd.buildingsmart.org");
                    Console.ResetColor();


                    IfdConceptList ifdConceptList = _bsdd.SearchProperties(pset.Name + "." + property.name);
                    if (ifdConceptList == null)
                    {
                        Console.WriteLine($"      Could not find the Property in bSDD");
                    }
                    else
                    {
                        IfdConcept bsddProperty = ifdConceptList.IfdConcept.FirstOrDefault();
                        Console.WriteLine($"      Loaded Property from bSDD");
                        Console.WriteLine($"      Guid:        {bsddProperty.Guid}");
                        Console.WriteLine($"      Status:      {bsddProperty.Status}");
                        Console.WriteLine($"      VersionDate: {bsddProperty.VersionDate}");
                        foreach (var item in bsddProperty.FullNames)
                        {
                            int l = item.Language.LanguageCode.Length;
                            string tab = new string(' ', 10 - l);
                            Console.WriteLine($"      Name {item.Language.LanguageCode}:{tab}{item.Name}", Encoding.UTF8);
                        }
                        Console.WriteLine($"      The GUID in the PSet file shall be changed {property.legacyGuidAsIfcGlobalId} => {bsddProperty.Guid}");
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"      OK: The GUID {property.legacyGuidAsIfcGlobalId} for {property.name} is properly resolved by http://bsdd.buildingsmart.org");
                    property.ifdGuid = property.legacyGuidAsIfcGlobalId;
                    Console.ResetColor();
                }

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
                        property.typeComplexProperty.subProperties = LoadProperties(pset,psetComplexProperty.PropertyDef); //Recursiv loading
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

                property.status = new PublicationStatus()
                {
                    versionNumber = 4,                 
                    dateOfVersion = new DateTime(2018, 1, 1),
                    revisionNumber = 2,
                    dateOfRevision = new DateTime(2018, 1, 1),
                    status = PublicationStatus.Status.Active.ToString(),
                    dateOfCreation = new DateTime(2018, 1, 1),
                    dateOfActivation = new DateTime(2018, 1, 1),
                    dateOfLastChange = new DateTime(2018, 1, 1),
                    languageOfCreator = "en-EN"
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

        private bool CheckGuidWithBsdd(string guid)
        {
            //guid = "3WcEc0qRmHuO00025QrE$V"; working GUID
            bool check = false;
            string url = $"http://bsdd.buildingsmart.org/api/4.0/IfdConcept/{guid}";

            var webClient = new WebClient();
            try
            {
                var response = webClient.DownloadString(url);
                check = true;
            }
            catch(Exception ex)
            {
                //Console.WriteLine($"      ERROR: {ex.Message} {url}");
                check = false;
            }

            return check;
        }
    }
}
