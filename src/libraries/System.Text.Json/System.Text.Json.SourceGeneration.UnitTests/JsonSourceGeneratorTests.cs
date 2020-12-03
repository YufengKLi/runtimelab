// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace System.Text.Json.SourceGeneration.UnitTests
{
    public class GeneratorTests
    {
        [Fact]
        public void TypeDiscoveryPrimitivePOCO()
        {
            string source = @"
            using System.Text.Json.Serialization;

            [assembly: JsonSerializable(typeof(HelloWorld.MyType))]

            namespace HelloWorld
            {
                public class MyType
                {
                    public int PublicPropertyInt { get; set; }
                    public string PublicPropertyString { get; set; }
                    private int PrivatePropertyInt { get; set; }
                    private string PrivatePropertyString { get; set; }

                    public double PublicDouble;
                    public char PublicChar;
                    private double PrivateDouble;
                    private char PrivateChar;

                    public void MyMethod() { }
                    public void MySecondMethod() { }

                    public void UsePrivates()
                    {
                        PrivateDouble = 0;
                        PrivateChar = ' ';
                        double d = PrivateDouble;
                        char c = PrivateChar;
                    }
                }
            }";

            Compilation compilation = CompilationHelper.CreateCompilation(source);

            JsonSourceGenerator generator = new JsonSourceGenerator();

            Compilation newCompilation = CompilationHelper.RunGenerators(compilation, out ImmutableArray<Diagnostic> generatorDiags, generator);

            // Make sure compilation was successful.
            CheckCompilationDiagnosticsErrors(generatorDiags);
            CheckCompilationDiagnosticsErrors(newCompilation.GetDiagnostics());

            // Check base functionality of found types.
            Assert.Equal(1, generator.SerializableTypes.Count);
            Type myType = generator.SerializableTypes["HelloWorld.MyType"];
            Assert.Equal("HelloWorld.MyType", myType.FullName);

            // Check for received fields, properties and methods in created type.
            string[] expectedPropertyNames = { "PublicPropertyInt", "PublicPropertyString",};
            string[] expectedFieldNames = { "PublicChar", "PublicDouble" };
            string[] expectedMethodNames = { "get_PrivatePropertyInt", "get_PrivatePropertyString", "get_PublicPropertyInt", "get_PublicPropertyString", "MyMethod", "MySecondMethod", "set_PrivatePropertyInt", "set_PrivatePropertyString", "set_PublicPropertyInt", "set_PublicPropertyString", "UsePrivates" };
            CheckFieldsPropertiesMethods("HelloWorld.MyType", ref generator, expectedFieldNames, expectedPropertyNames, expectedMethodNames);
        }

        [Fact]
        public void TypeDiscoveryPrimitiveExternalPOCO()
        {
            // Compile the referenced assembly first.
            Compilation referencedCompilation = CompilationHelper.CreateReferencedLocationCompilation();

            // Emit the image of the referenced assembly.
            byte[] referencedImage = CompilationHelper.CreateAssemblyImage(referencedCompilation);

            string source = @"
            using System.Text.Json.Serialization;
            using ReferencedAssembly;

            [assembly: JsonSerializable(typeof(HelloWorld.MyType))]
            [assembly: JsonSerializable(typeof(ReferencedAssembly.Location))]

            namespace HelloWorld
            {
                public class MyType
                {
                    public int PublicPropertyInt { get; set; }
                    public string PublicPropertyString { get; set; }
                    private int PrivatePropertyInt { get; set; }
                    private string PrivatePropertyString { get; set; }

                    public double PublicDouble;
                    public char PublicChar;
                    private double PrivateDouble;
                    private char PrivateChar;

                    public void MyMethod() { }
                    public void MySecondMethod() { }
                    public void UsePrivates()
                    {
                        PrivateDouble = 0;
                        PrivateChar = ' ';
                        double x = PrivateDouble;
                        string s = PrivateChar.ToString();
                    }
                }
            }";

            MetadataReference[] additionalReferences = { MetadataReference.CreateFromImage(referencedImage) };

            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences);

            JsonSourceGenerator generator = new JsonSourceGenerator();

            Compilation newCompilation = CompilationHelper.RunGenerators(compilation, out ImmutableArray<Diagnostic> generatorDiags, generator);

            // Make sure compilation was successful.
            CheckCompilationDiagnosticsErrors(generatorDiags);
            CheckCompilationDiagnosticsErrors(newCompilation.GetDiagnostics());

            // Check base functionality of found types.
            Assert.Equal(2, generator.SerializableTypes.Count);
            Type myType = generator.SerializableTypes["HelloWorld.MyType"];
            Type notMyType = generator.SerializableTypes["ReferencedAssembly.Location"];

            // Check for MyType.
            Assert.Equal("HelloWorld.MyType", myType.FullName);

            // Check for received fields, properties and methods for MyType.
            string[] expectedFieldNamesMyType = { "PublicChar", "PublicDouble" };
            string[] expectedPropertyNamesMyType = { "PublicPropertyInt", "PublicPropertyString" };
            string[] expectedMethodNamesMyType = { "get_PrivatePropertyInt", "get_PrivatePropertyString", "get_PublicPropertyInt", "get_PublicPropertyString", "MyMethod", "MySecondMethod", "set_PrivatePropertyInt", "set_PrivatePropertyString", "set_PublicPropertyInt", "set_PublicPropertyString", "UsePrivates" };
            CheckFieldsPropertiesMethods("HelloWorld.MyType", ref generator, expectedFieldNamesMyType, expectedPropertyNamesMyType, expectedMethodNamesMyType);

            // Check for NotMyType.
            Assert.Equal("ReferencedAssembly.Location", notMyType.FullName);

            // Check for received fields, properties and methods for NotMyType.
            string[] expectedFieldNamesNotMyType = { };
            string[] expectedPropertyNamesNotMyType = { "Address1", "Address2", "City", "Country", "Id", "Name", "PhoneNumber", "PostalCode", "State" };
            string[] expectedMethodNamesNotMyType = { "get_Address1", "get_Address2", "get_City", "get_Country", "get_Id", "get_Name", "get_PhoneNumber", "get_PostalCode", "get_State",
                                                      "set_Address1", "set_Address2", "set_City", "set_Country", "set_Id", "set_Name", "set_PhoneNumber", "set_PostalCode", "set_State" };
            CheckFieldsPropertiesMethods("ReferencedAssembly.Location", ref generator, expectedFieldNamesNotMyType, expectedPropertyNamesNotMyType, expectedMethodNamesNotMyType);
        }

        [Fact]
        public void TypeDiscoveryWithRenamedAttribute()
        {
            // Compile the referenced assembly first.
            Compilation referencedCompilation = CompilationHelper.CreateReferencedLocationCompilation();

            // Emit the image of the referenced assembly.
            byte[] referencedImage = CompilationHelper.CreateAssemblyImage(referencedCompilation);
            MetadataReference[] additionalReferences = { MetadataReference.CreateFromImage(referencedImage) };

            string source = @"
            using System.Text.Json.Serialization;
            using ReferencedAssembly;

            using @JsonSerializable = System.Runtime.Serialization.ContractNamespaceAttribute;
            using AliasedAttribute = System.Text.Json.Serialization.JsonSerializableAttribute;

            [assembly: AliasedAttribute(typeof(HelloWorld.MyType))]
            [assembly: AliasedAttribute(typeof(ReferencedAssembly.Location))]
            [module: @JsonSerializable(""my namespace"")]

            namespace HelloWorld
            {
                public class MyType
                {
                    public int PublicPropertyInt { get; set; }
                    public string PublicPropertyString { get; set; }
                    private int PrivatePropertyInt { get; set; }
                    private string PrivatePropertyString { get; set; }

                    public double PublicDouble;
                    public char PublicChar;
                    private double PrivateDouble;
                    private char PrivateChar;

                    public void MyMethod() { }
                    public void MySecondMethod() { }
                    public void UsePrivates()
                    {
                        PrivateDouble = 0;
                        PrivateChar = ' ';
                        double d = PrivateDouble;
                        char c = PrivateChar;
                    }
                }
            }";

            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences);

            JsonSourceGenerator generator = new JsonSourceGenerator();

            Compilation newCompilation = CompilationHelper.RunGenerators(compilation, out var generatorDiags, generator);

            // Make sure compilation was successful.
            CheckCompilationDiagnosticsErrors(generatorDiags);
            CheckCompilationDiagnosticsErrors(newCompilation.GetDiagnostics());

            // Check base functionality of found types.
            Assert.Equal(2, generator.SerializableTypes.Count);

            // Check for MyType.
            Assert.Equal("HelloWorld.MyType", generator.SerializableTypes["HelloWorld.MyType"].FullName);

            // Check for received fields, properties and methods for MyType.
            string[] expectedFieldNamesMyType = { "PublicChar", "PublicDouble" };
            string[] expectedPropertyNamesMyType = { "PublicPropertyInt", "PublicPropertyString" };
            string[] expectedMethodNamesMyType = { "get_PrivatePropertyInt", "get_PrivatePropertyString", "get_PublicPropertyInt", "get_PublicPropertyString", "MyMethod", "MySecondMethod", "set_PrivatePropertyInt", "set_PrivatePropertyString", "set_PublicPropertyInt", "set_PublicPropertyString", "UsePrivates" };
            CheckFieldsPropertiesMethods("HelloWorld.MyType", ref generator, expectedFieldNamesMyType, expectedPropertyNamesMyType, expectedMethodNamesMyType);

            // Check for NotMyType.
            Assert.Equal("ReferencedAssembly.Location", generator.SerializableTypes["ReferencedAssembly.Location"].FullName);

            // Check for received fields, properties and methods for NotMyType.
            string[] expectedFieldNamesNotMyType = { };
            string[] expectedPropertyNamesNotMyType = { "Address1", "Address2", "City", "Country", "Id", "Name", "PhoneNumber", "PostalCode", "State" };
            string[] expectedMethodNamesNotMyType = { "get_Address1", "get_Address2", "get_City", "get_Country", "get_Id", "get_Name", "get_PhoneNumber", "get_PostalCode", "get_State",
                                                      "set_Address1", "set_Address2", "set_City", "set_Country", "set_Id", "set_Name", "set_PhoneNumber", "set_PostalCode", "set_State" };
            CheckFieldsPropertiesMethods("ReferencedAssembly.Location", ref generator, expectedFieldNamesNotMyType, expectedPropertyNamesNotMyType, expectedMethodNamesNotMyType );
        }

        // TODO: add test where there is a local invalid System.Text.Json.JsonSerializableAttribute (https://github.com/dotnet/runtimelab/issues/29)

        [Fact]
        public void NameClashCompilation()
        {
            Compilation compilation = CompilationHelper.CreateRepeatedLocationsCompilation();

            JsonSourceGenerator generator = new JsonSourceGenerator();

            Compilation newCompilation = CompilationHelper.RunGenerators(compilation, out var generatorDiags, generator);

            // Make sure compilation was successful.
            CheckCompilationDiagnosticsErrors(generatorDiags);
            CheckCompilationDiagnosticsErrors(newCompilation.GetDiagnostics());
        }

        [Fact]
        public void CollectionDictionarySourceGeneration()
        {
            // Compile the referenced assembly first.
            Compilation referencedCompilation = CompilationHelper.CreateReferencedHighLowTempsCompilation();

            // Emit the image of the referenced assembly.
            byte[] referencedImage = CompilationHelper.CreateAssemblyImage(referencedCompilation);

            string source = @"
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using System.Text.Json.Serialization;
            using ReferencedAssembly;

            [assembly: JsonSerializable(typeof(HelloWorld.WeatherForecastWithPOCOs))]
    
            namespace HelloWorld
            {
                public class WeatherForecastWithPOCOs
                {
                    public DateTimeOffset Date { get; set; }
                    public int TemperatureCelsius { get; set; }
                    public string Summary { get; set; }
                    public string SummaryField;
                    public List<DateTimeOffset> DatesAvailable { get; set; }
                    public Dictionary<string, HighLowTemps> TemperatureRanges { get; set; }
                    public string[] SummaryWords { get; set; }
                }
            }";

            MetadataReference[] additionalReferences = { MetadataReference.CreateFromImage(referencedImage) };

            Compilation compilation = CompilationHelper.CreateCompilation(source, additionalReferences);

            JsonSourceGenerator generator = new JsonSourceGenerator();

            Compilation newCompilation = CompilationHelper.RunGenerators(compilation, out var generatorDiags, generator);

            // Make sure compilation was successful.

            CheckCompilationDiagnosticsErrors(generatorDiags);
            CheckCompilationDiagnosticsErrors(newCompilation.GetDiagnostics());
        }

        private void CheckCompilationDiagnosticsErrors(ImmutableArray<Diagnostic> diagnostics)
        {
            Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        private void CheckFieldsPropertiesMethods(string typeName, ref JsonSourceGenerator generator, string[] expectedFields, string[] expectedProperties, string[] expectedMethods)
        {
            string[] receivedFields = generator.SerializableTypes[typeName].GetFields().Select(field => field.Name).OrderBy(s => s).ToArray();
            string[] receivedProperties = generator.SerializableTypes[typeName].GetProperties().Select(property => property.Name).OrderBy(s => s).ToArray();
            string[] receivedMethods = generator.SerializableTypes[typeName].GetMethods().Select(method => method.Name).OrderBy(s => s).ToArray();

            Assert.Equal(expectedFields, receivedFields);
            Assert.Equal(expectedProperties, receivedProperties);
            Assert.Equal(expectedMethods, receivedMethods);
        }
    }
}
