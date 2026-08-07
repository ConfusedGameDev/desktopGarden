using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Tests
{
    /// <summary>
    /// Runs the EditMode suite from a menu item and writes a one-line summary (plus failures) to
    /// <c>Logs/editmode-menu.txt</c>. Exists because batchmode <c>-runTests</c> needs the editor
    /// closed, while tooling driving the *open* editor (MCP) can only trigger menu items.
    /// </summary>
    internal static class EditModeTestMenu
    {
        public const string ResultsPath = "Logs/editmode-menu.txt";

        [MenuItem("Pollen Garden/Run EditMode Tests")]
        public static void Run()
        {
            System.IO.File.Delete(ResultsPath);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new FileResultWriter());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        private sealed class FileResultWriter : ICallbacks
        {
            private readonly StringBuilder failures = new StringBuilder();

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren && result.TestStatus == TestStatus.Failed)
                {
                    failures.AppendLine("FAILED: " + result.FullName);
                    failures.AppendLine(result.Message);
                }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                string summary = "RESULT: " + result.TestStatus
                    + " passed=" + result.PassCount
                    + " failed=" + result.FailCount
                    + " skipped=" + result.SkipCount + "\n" + failures;
                System.IO.File.WriteAllText(ResultsPath, summary);
            }
        }
    }
}
