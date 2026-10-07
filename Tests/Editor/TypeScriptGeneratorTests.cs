#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Ceffy.Bridge;
using Ceffy.Bridge.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Ceffy.Tests.Editor
{
    public class TypeScriptGeneratorTests
    {
        [Test]
        public void BuildTypeScript_EmitsEmptyBridgeAsModule()
        {
            var generated = TypeScriptGenerator.BuildTypeScript(
                new List<Type>(), new List<Type>(), new HashSet<Type>());

            StringAssert.Contains("declare global {", generated);
            StringAssert.Contains("interface Window {", generated);
            StringAssert.Contains("unity: {", generated);
            StringAssert.Contains("ui: {", generated);
            StringAssert.Contains("export {}", generated);
        }

        [Test]
        public void WalkInterface_DiscoversParameterAndReturnModels()
        {
            CollectionAssert.AreEquivalent(
                new[] { typeof(VisibleDependency), typeof(NullableModel), typeof(TestState) },
                Discover(typeof(IDependencyMethods)));
        }

        [Test]
        public void WalkInterface_PrunesIgnoredFieldsAndProperties()
        {
            CollectionAssert.AreEquivalent(
                new[] { typeof(IgnoredModel), typeof(VisibleDependency) },
                Discover(typeof(IModelMethods<IgnoredModel>)));
        }

        [Test]
        public void BuildTypeScript_PrunesIgnoredFieldsAndProperties()
        {
            var generated = GenerateModel(typeof(IgnoredModel));

            StringAssert.Contains("visible?: VisibleDependency;", generated);
            StringAssert.DoesNotContain("ignoredField", generated);
            StringAssert.DoesNotContain("ignoredProperty", generated);
            StringAssert.DoesNotContain("ignoredDataField", generated);
            StringAssert.DoesNotContain("ignoredDataProperty", generated);
            StringAssert.DoesNotContain(nameof(HiddenDependency), generated);
        }

        [Test]
        public void BuildTypeScript_UsesOptInIncludingAttributedPrivateMembers()
        {
            var generated = GenerateModel(typeof(OptInModel));

            StringAssert.Contains("\"friendly-name\"?: string;", generated);
            StringAssert.Contains("PrivateValue?: string;", generated);
            StringAssert.Contains("\"serialized-field\"?: string;", generated);
            StringAssert.DoesNotContain("unmarkedField", generated);
            StringAssert.DoesNotContain("unmarkedProperty", generated);
            StringAssert.DoesNotContain("ignoredOptIn", generated);
            StringAssert.DoesNotContain(nameof(HiddenDependency), generated);
        }

        [Test]
        public void BuildTypeScript_RespectsDataContractOptInAndNames()
        {
            var generated = GenerateModel(typeof(DataContractModel));

            StringAssert.Contains("WireName?: string;", generated);
            StringAssert.DoesNotContain("unmarked", generated);
            StringAssert.DoesNotContain(nameof(HiddenDependency), generated);
        }

        [Test]
        public void BuildTypeScript_RespectsFieldsContract()
        {
            var generated = GenerateModel(typeof(FieldsModel));

            StringAssert.Contains("visibleField: number;", generated);
            StringAssert.DoesNotContain("excludedProperty", generated);
            StringAssert.DoesNotContain(nameof(HiddenDependency), generated);
        }

        [Test]
        public void BuildTypeScript_MatchesRuntimeSerializedNames()
        {
            var serialized = JObject.Parse(JsonConvert.SerializeObject(new NamingModel(), BridgeSerializer.Settings));
            var generated = GenerateModel(typeof(NamingModel));

            Assert.IsNotNull(serialized["displayName"]);
            Assert.IsNotNull(serialized["urlValue"]);
            Assert.IsNotNull(serialized["EXACT-Name"]);
            StringAssert.Contains("displayName?: string;", generated);
            StringAssert.Contains("urlValue?: string;", generated);
            StringAssert.Contains("\"EXACT-Name\"?: string;", generated);
        }

        [Test]
        public void BuildTypeScript_EscapesJsonPropertyNames()
        {
            var generated = GenerateModel(typeof(EscapedNameModel));

            StringAssert.Contains(JsonConvert.ToString("quote\"slash\\line\n") + "?: string;", generated);
        }

        [Test]
        public void BuildTypeScript_SkipsOverloadedIndexersAndTheirDependencies()
        {
            var generated = GenerateModel(typeof(IndexerModel));

            StringAssert.Contains("name?: string;", generated);
            StringAssert.DoesNotContain("item", generated);
            StringAssert.DoesNotContain(nameof(HiddenDependency), generated);
        }

        [TestCase(typeof(VisibleDependency[]))]
        [TestCase(typeof(List<VisibleDependency>))]
        [TestCase(typeof(IReadOnlyList<VisibleDependency>))]
        [TestCase(typeof(IReadOnlyCollection<VisibleDependency>))]
        [TestCase(typeof(IList<VisibleDependency>))]
        [TestCase(typeof(ICollection<VisibleDependency>))]
        [TestCase(typeof(IEnumerable<VisibleDependency>))]
        [TestCase(typeof(HashSet<VisibleDependency>))]
        public void BuildTypeScript_MapsCollectionsToArrays(Type collectionType)
        {
            var iface = typeof(IModelMethods<>).MakeGenericType(collectionType);
            var generated = GenerateFor(iface);

            CollectionAssert.AreEquivalent(new[] { typeof(VisibleDependency) }, Discover(iface));
            StringAssert.Contains("read(): Promise<VisibleDependency[]>;", generated);
            StringAssert.DoesNotContain("`", generated);
        }

        [TestCase(typeof(Dictionary<TestState, VisibleDependency>))]
        [TestCase(typeof(IDictionary<TestState, VisibleDependency>))]
        [TestCase(typeof(IReadOnlyDictionary<TestState, VisibleDependency>))]
        public void BuildTypeScript_DiscoversDictionaryKeysAndValues(Type dictionaryType)
        {
            var iface = typeof(IModelMethods<>).MakeGenericType(dictionaryType);
            var generated = GenerateFor(iface);

            CollectionAssert.AreEquivalent(new[] { typeof(TestState), typeof(VisibleDependency) }, Discover(iface));
            StringAssert.Contains("read(): Promise<Record<TestState, VisibleDependency>>;", generated);
            StringAssert.DoesNotContain("`", generated);
        }

        [Test]
        public void BuildTypeScript_PreservesDictionaryKeyCasing()
        {
            var serialized = JObject.Parse(
                JsonConvert.SerializeObject(new DictionaryModel(), BridgeSerializer.Settings));
            var generated = GenerateModel(typeof(DictionaryModel));

            Assert.IsNotNull(serialized["values"]?["UPPER_Key"]);
            Assert.IsNull(serialized["values"]?["uppeR_Key"]);
            StringAssert.Contains("values?: Record<string, VisibleDependency>;", generated);
        }

        [Test]
        public void WalkInterface_TerminatesRecursiveModelGraphs()
        {
            var generated = GenerateModel(typeof(RecursiveModel));

            CollectionAssert.AreEquivalent(
                new[] { typeof(RecursiveModel) }, Discover(typeof(IModelMethods<RecursiveModel>)));
            StringAssert.Contains("next?: RecursiveModel | null;", generated);
            StringAssert.Contains("children?: RecursiveModel[];", generated);
            Assert.AreEqual(1, CountOccurrences(generated, "export interface RecursiveModel"));
        }

        [Test]
        public void BuildTypeScript_PreservesNullableFieldsAndProperties()
        {
            var generated = GenerateModel(typeof(NullableModel));

            StringAssert.Contains("nullableField?: string | null;", generated);
            StringAssert.Contains("nullableName?: string | null;", generated);
            StringAssert.Contains("nullableReference?: VisibleDependency | null;", generated);
            StringAssert.Contains("nullableCount?: number | null;", generated);
            StringAssert.Contains("nullableState?: TestState | null;", generated);
            StringAssert.Contains("name?: string;", generated);
        }

        [Test]
        public void BuildTypeScript_PreservesNullableCollectionsAndElements()
        {
            var generated = GenerateModel(typeof(NullableCollectionsModel));

            StringAssert.Contains("nullableArray?: string[] | null;", generated);
            StringAssert.Contains("nullableElements?: (string | null)[];", generated);
            StringAssert.Contains("nullableList?: (VisibleDependency | null)[] | null;", generated);
            StringAssert.Contains("nullableNumbers?: (number | null)[];", generated);
            StringAssert.Contains("nested?: ((string | null)[] | null)[] | null;", generated);
            StringAssert.Contains("values?: Record<string, VisibleDependency | null> | null;", generated);
            StringAssert.Contains("nestedValues?: Record<string, (string | null)[] | null> | null;", generated);
        }

        [Test]
        public void BuildTypeScript_UsesNullableContextForNestedMembers()
        {
            var generated = GenerateModel(typeof(NullableContextModel));

            StringAssert.Contains("values?: string[];", generated);
            StringAssert.Contains("nullableValues?: (string | null)[] | null;", generated);
        }

        [Test]
        public void BuildTypeScript_ReadsInheritedPrivateMemberNullability()
        {
            var generated = GenerateModel(typeof(InheritedModel));

            StringAssert.Contains("inheritedValue?: string | null;", generated);
            StringAssert.Contains("value: number;", generated);
        }

        [Test]
        public void BuildTypeScript_UsesNullHandlingAndRequiredOverrides()
        {
            var generated = GenerateModel(typeof(NullHandlingModel));

            StringAssert.Contains("ignoredNull?: string | null;", generated);
            StringAssert.Contains("includedNull: string | null;", generated);
            StringAssert.Contains("requiredValue: string;", generated);
            StringAssert.Contains("requiredNullable: string | null;", generated);
            StringAssert.Contains("disallowedNull?: string;", generated);
            StringAssert.Contains("count: number;", generated);
        }

        [Test]
        public void BuildTypeScript_MatchesRuntimeNullOmission()
        {
            var serialized = JObject.Parse(
                JsonConvert.SerializeObject(new NullHandlingModel(), BridgeSerializer.Settings));

            Assert.IsNull(serialized["ignoredNull"]);
            Assert.AreEqual(JTokenType.Null, serialized["includedNull"]!.Type);
            Assert.AreEqual(JTokenType.Integer, serialized["count"]!.Type);
        }

        [Test]
        public void BuildTypeScript_EmitsDateTimeAndIdentifiersAsJsonStrings()
        {
            var generated = GenerateModel(typeof(WireValueModel));

            StringAssert.Contains("timestamp: string;", generated);
            StringAssert.Contains("offsetTimestamp: string;", generated);
            StringAssert.Contains("nullableTimestamp?: string | null;", generated);
            StringAssert.Contains("id: string;", generated);
            StringAssert.DoesNotContain("Date;", generated);
            StringAssert.DoesNotContain("export interface DateTime", generated);
            StringAssert.DoesNotContain("export interface Guid", generated);
        }

        [TestCase(typeof(byte), "number")]
        [TestCase(typeof(sbyte), "number")]
        [TestCase(typeof(short), "number")]
        [TestCase(typeof(ushort), "number")]
        [TestCase(typeof(int), "number")]
        [TestCase(typeof(uint), "number")]
        [TestCase(typeof(long), "number")]
        [TestCase(typeof(ulong), "number")]
        [TestCase(typeof(float), "number")]
        [TestCase(typeof(double), "number")]
        [TestCase(typeof(decimal), "number")]
        [TestCase(typeof(bool), "boolean")]
        [TestCase(typeof(string), "string")]
        [TestCase(typeof(char), "string")]
        [TestCase(typeof(Guid), "string")]
        [TestCase(typeof(DateTime), "string")]
        [TestCase(typeof(DateTimeOffset), "string")]
        [TestCase(typeof(byte[]), "string")]
        public void BuildTypeScript_MapsJsonPrimitiveWireTypes(Type type, string expectedType)
        {
            var iface = typeof(IModelMethods<>).MakeGenericType(type);

            Assert.IsEmpty(Discover(iface));
            StringAssert.Contains($"read(): Promise<{expectedType}>;", GenerateFor(iface));
        }

        [Test]
        public void BuildTypeScript_PreservesNumericEnumValuesAndAliases()
        {
            var generated = GenerateModel(typeof(TestState));

            StringAssert.Contains("Negative = -3,", generated);
            StringAssert.Contains("Unknown = 4,", generated);
            StringAssert.Contains("Ready = 9,", generated);
            StringAssert.Contains("Alias = 9,", generated);
        }

        [Test]
        public void BuildTypeScript_PreservesNullableParametersAndReturns()
        {
            var generated = GenerateFor(typeof(INullableMethods));

            StringAssert.Contains("find(name: string | null): Promise<NullableModel | null>;", generated);
            StringAssert.Contains("count(value: number | null): Promise<number | null>;", generated);
            StringAssert.Contains("name(): Promise<string | null>;", generated);
            StringAssert.Contains("syncName(): Promise<string | null>;", generated);
            StringAssert.Contains(
                "list(values: (string | null)[] | null): Promise<(string | null)[] | null>;", generated);
            StringAssert.Contains("optional(value?: string | null): Promise<void>;", generated);
        }

        [Test]
        public void BuildTypeScript_HandlesSynchronousAndAsynchronousUnityMethods()
        {
            var generated = GenerateFor(typeof(IUnityReturnMethods));

            StringAssert.Contains("refresh(): Promise<void>;", generated);
            StringAssert.Contains("notify(): Promise<void>;", generated);
            StringAssert.Contains("load(): Promise<number>;", generated);
            StringAssert.Contains("get(): Promise<boolean>;", generated);
        }

        [Test]
        public void BuildTypeScript_AllowsPromisesAndImmediateResultsForTaskUiHandlers()
        {
            var generated = GenerateFor(null, typeof(IUiReturnMethods));

            StringAssert.Contains(
                "save: ((count: number | null) => number | null | Promise<number | null>) | null;", generated);
            StringAssert.Contains("refresh: (() => void | Promise<void>) | null;", generated);
            StringAssert.Contains("show: ((name: string | null) => void) | null;", generated);
            StringAssert.Contains("read: (() => boolean) | null;", generated);
            StringAssert.Contains(
                "find: (() => VisibleDependency | null | Promise<VisibleDependency | null>) | null;", generated);
        }

        [Test]
        public void BuildTypeScript_PreservesCamelCaseMethodNames()
        {
            var generated = GenerateFor(typeof(INamingMethods), typeof(INamingMethods));

            StringAssert.Contains("getEmployeeActivity(employeeId: number): Promise<void>;", generated);
            StringAssert.Contains("getEmployeeActivity: ((employeeId: number) => void) | null;", generated);
            StringAssert.Contains("urlChanged(urlValue: string): Promise<void>;", generated);
        }

        [Test]
        public void BuildTypeScript_CombinesMultipleViewsWithoutDuplicateModels()
        {
            var unityMethods = new List<Type> { typeof(IDependencyMethods), typeof(INamingMethods) };
            var uiMethods = new List<Type> { typeof(IUiReturnMethods), typeof(INamingMethods) };
            var models = Discover(typeof(IDependencyMethods));
            TypeScriptGenerator.WalkInterface(typeof(IUiReturnMethods), models);
            var generated = TypeScriptGenerator.BuildTypeScript(unityMethods, uiMethods, models);

            Assert.AreEqual(1, CountOccurrences(generated, "export interface VisibleDependency"));
            StringAssert.Contains("find:", generated);
            StringAssert.Contains("getEmployeeActivity(", generated);
        }

        [Test]
        public void BuildTypeScript_ProducesStableAlphabeticalModelOrder()
        {
            var unityMethods = new List<Type> { typeof(IDependencyMethods) };
            var first = new HashSet<Type> { typeof(VisibleDependency), typeof(TestState), typeof(NullableModel) };
            var second = new HashSet<Type> { typeof(NullableModel), typeof(TestState), typeof(VisibleDependency) };
            var generated = TypeScriptGenerator.BuildTypeScript(unityMethods, new List<Type>(), first);

            Assert.AreEqual(generated, TypeScriptGenerator.BuildTypeScript(unityMethods, new List<Type>(), second));
            Assert.Less(generated.IndexOf("export interface NullableModel", StringComparison.Ordinal),
                generated.IndexOf("export enum TestState", StringComparison.Ordinal));
            Assert.Less(generated.IndexOf("export enum TestState", StringComparison.Ordinal),
                generated.IndexOf("export interface VisibleDependency", StringComparison.Ordinal));
        }

        private static HashSet<Type> Discover(Type iface)
        {
            var models = new HashSet<Type>();
            TypeScriptGenerator.WalkInterface(iface, models);
            return models;
        }

        private static string GenerateModel(Type model)
        {
            return GenerateFor(typeof(IModelMethods<>).MakeGenericType(model));
        }

        private static string GenerateFor(Type? unityInterface, Type? uiInterface = null)
        {
            var unityMethods = new List<Type>();
            var uiMethods = new List<Type>();
            var models = new HashSet<Type>();
            if (unityInterface != null)
            {
                unityMethods.Add(unityInterface);
                TypeScriptGenerator.WalkInterface(unityInterface, models);
            }
            if (uiInterface != null)
            {
                uiMethods.Add(uiInterface);
                TypeScriptGenerator.WalkInterface(uiInterface, models);
            }

            return TypeScriptGenerator.BuildTypeScript(unityMethods, uiMethods, models);
        }

        private static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var position = 0;
            while ((position = text.IndexOf(value, position, StringComparison.Ordinal)) >= 0)
            {
                count++;
                position += value.Length;
            }

            return count;
        }

        private interface IModelMethods<T>
        {
            Task<T> Read();
        }

        private interface IDependencyMethods
        {
            Task<VisibleDependency> Load(NullableModel model, TestState state);
        }

        private interface INullableMethods
        {
            Task<NullableModel?> Find(string? name);
            Task<int?> Count(int? value);
            Task<string?> Name();
            string? SyncName();
            Task<IReadOnlyList<string?>?> List(IReadOnlyList<string?>? values);
            Task Optional(string? value = null);
        }

        private interface IUnityReturnMethods
        {
            Task Refresh();
            void Notify();
            Task<int> Load();
            bool Get();
        }

        private interface IUiReturnMethods
        {
            Task<int?> Save(int? count);
            Task Refresh();
            void Show(string? name);
            bool Read();
            Task<VisibleDependency?> Find();
        }

        private interface INamingMethods
        {
            void GetEmployeeActivity(int employeeId);
            void URLChanged(string urlValue);
        }

        private class IgnoredModel
        {
            [JsonIgnore]
            public HiddenDependency ignoredField = null!;

            [IgnoreDataMember]
            public HiddenDependency ignoredDataField = null!;

            public VisibleDependency Visible { get; set; } = new VisibleDependency();

            [JsonIgnore]
            public HiddenDependency IgnoredProperty { get; set; } = null!;

            [IgnoreDataMember]
            public HiddenDependency IgnoredDataProperty { get; set; } = null!;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class OptInModel
        {
            public HiddenDependency unmarkedField = null!;

            [JsonProperty("serialized-field")]
            public string serializedField = string.Empty;

            [JsonProperty("friendly-name")]
            public string FriendlyName { get; set; } = string.Empty;

            [JsonProperty("PrivateValue")]
            private string PrivateValue { get; set; } = string.Empty;

            public HiddenDependency UnmarkedProperty { get; set; } = null!;

            [JsonProperty, JsonIgnore]
            public HiddenDependency IgnoredOptIn { get; set; } = null!;
        }

        [DataContract]
        private class DataContractModel
        {
            [DataMember(Name = "WireName")]
            public string Value { get; set; } = string.Empty;

            public HiddenDependency Unmarked { get; set; } = null!;
        }

        [JsonObject(MemberSerialization.Fields)]
        private class FieldsModel
        {
            public int visibleField = 1;

            public HiddenDependency ExcludedProperty => new HiddenDependency();
        }

        private class NamingModel
        {
            public string DisplayName { get; set; } = string.Empty;
            public string URLValue { get; set; } = string.Empty;

            [JsonProperty("EXACT-Name")]
            public string ExactName { get; set; } = string.Empty;
        }

        private class EscapedNameModel
        {
            [JsonProperty("quote\"slash\\line\n")]
            public string Value { get; set; } = string.Empty;
        }

        private class IndexerModel
        {
            public HiddenDependency this[int index] => new HiddenDependency();
            public HiddenDependency this[string index] => new HiddenDependency();
            public string Name { get; set; } = string.Empty;
        }

        private class DictionaryModel
        {
            public Dictionary<string, VisibleDependency> Values { get; set; } =
                new Dictionary<string, VisibleDependency> { { "UPPER_Key", new VisibleDependency() } };
        }

        private class RecursiveModel
        {
            public RecursiveModel? Next { get; set; }
            public List<RecursiveModel> Children { get; set; } = new List<RecursiveModel>();
        }

        private class NullableModel
        {
            public string? nullableField;

            public string? NullableName { get; set; }
            public VisibleDependency? NullableReference { get; set; }
            public int? NullableCount { get; set; }
            public TestState? NullableState { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        private class NullableCollectionsModel
        {
            public string[]? NullableArray { get; set; }
            public string?[] NullableElements { get; set; } = Array.Empty<string?>();
            public IReadOnlyList<VisibleDependency?>? NullableList { get; set; }
            public List<int?> NullableNumbers { get; set; } = new List<int?>();
            public List<List<string?>?>? Nested { get; set; }
            public Dictionary<string, VisibleDependency?>? Values { get; set; }
            public Dictionary<string, List<string?>?>? NestedValues { get; set; }
        }

        private class NullableContextModel
        {
            public List<string> Values { get; set; } = new List<string>();
            public List<string?>? NullableValues { get; set; }
        }

        private class InheritedBaseModel
        {
            [JsonProperty("inheritedValue")]
            private string? InheritedValue { get; set; }
        }

        private class InheritedModel : InheritedBaseModel
        {
            public int Value { get; set; }
        }

        private class NullHandlingModel
        {
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string? IgnoredNull { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Include)]
            public string? IncludedNull { get; set; }

            [JsonProperty(Required = Required.Always)]
            public string RequiredValue { get; set; } = string.Empty;

            [JsonProperty(Required = Required.AllowNull)]
            public string RequiredNullable { get; set; } = string.Empty;

            [JsonProperty(Required = Required.DisallowNull)]
            public string DisallowedNull { get; set; } = string.Empty;

            public int Count { get; set; }
        }

        private class WireValueModel
        {
            public DateTime Timestamp { get; set; }
            public DateTimeOffset OffsetTimestamp { get; set; }
            public DateTime? NullableTimestamp { get; set; }
            public Guid Id { get; set; }
        }

        private class VisibleDependency
        {
            public int Value { get; set; }
        }

        private class HiddenDependency
        {
            public string Value { get; set; } = string.Empty;
        }

        private enum TestState
        {
            Negative = -3,
            Unknown = 4,
            Ready = 9,
            Alias = 9,
        }
    }
}
