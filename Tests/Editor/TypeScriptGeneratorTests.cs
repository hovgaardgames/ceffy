using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ceffy.Bridge.Editor;
using NUnit.Framework;

namespace Ceffy.Tests.Editor
{
    public class TypeScriptGeneratorTests
    {
        [Test]
        public void BuildTypeScript_EmitsInterfacesAndReferencedModels()
        {
            var unityMethods = new List<Type> { typeof(ITestUnityMethods) };
            var uiMethods = new List<Type> { typeof(ITestUiMethods) };
            var models = new HashSet<Type> { typeof(TestModel), typeof(TestState) };

            var generated = TypeScriptGenerator.BuildTypeScript(unityMethods, uiMethods, models);
            StringAssert.Contains("export interface TestModel", generated);
            StringAssert.Contains("displayName: string;", generated);
            StringAssert.Contains("state: TestState;", generated);
            StringAssert.Contains("optionalCount: number | null;", generated);
            StringAssert.Contains("aliases: string[];", generated);
            StringAssert.Contains("states: TestState[];", generated);
            StringAssert.Contains("models: Record<string, TestModel>;", generated);
            StringAssert.Contains("loadModel(accountId: number): Promise<TestModel>;", generated);
            StringAssert.Contains("loadModels(): Promise<TestModel[]>;", generated);
            StringAssert.Contains("refresh(): Promise<void>;", generated);
            StringAssert.Contains("showState: ((state: TestState) => void) | null;", generated);
        }

        private interface ITestUnityMethods
        {
            Task<TestModel> LoadModel(int accountId);
            Task<List<TestModel>> LoadModels();
            Task Refresh();
        }

        private interface ITestUiMethods
        {
            void ShowState(TestState state);
        }

        private class TestModel
        {
            public string DisplayName { get; set; }
            public TestState State { get; set; }
            public int? OptionalCount { get; set; }
            public string[] Aliases { get; set; }
            public List<TestState> States { get; set; }
            public Dictionary<string, TestModel> Models { get; set; }
        }

        private enum TestState
        {
            Unknown,
            Ready
        }
    }
}
