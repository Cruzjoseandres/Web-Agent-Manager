using System;
using System.Collections.Generic;

namespace WebAgentManagerApp.Tests
{
    public static class UiPresentationHelperTests
    {
        private static int failures;

        public static int Main()
        {
            Run("Agrega http a una IP sin esquema", TestAddsHttpToRawAddress);
            Run("Conserva direcciones http y https", TestPreservesSupportedSchemes);
            Run("Rechaza esquemas que no son web", TestRejectsUnsupportedSchemes);
            Run("Busca todos los ambientes al coincidir el cliente", TestClientNameMatchReturnsAllEnvironments);
            Run("Busca ambientes por nombre sin distinguir mayusculas", TestFiltersByEnvironmentName);
            Run("Busca ambientes por IP parcial", TestFiltersByPartialIp);
            Run("Determina si un cliente coincide con cualquier campo buscable", TestMatchesClientOrEnvironment);
            Run("No modifica la coleccion original al filtrar", TestFilteringDoesNotMutateClient);
            Run("Expande ambientes mientras existe una busqueda", TestSearchTemporarilyExpandsClients);
            Run("Parsea direccion de servidor separando host, puerto y esquema", TestParseServerAddress);
            Run("Concatena direccion web con https o http y sufijo webapp", TestBuildServerAddress);

            if (failures == 0)
            {
                Console.WriteLine("OK: 11 pruebas superadas.");
                return 0;
            }

            Console.Error.WriteLine("ERROR: " + failures + " prueba(s) fallaron.");
            return 1;
        }

        private static void TestAddsHttpToRawAddress()
        {
            AssertEqual("http://192.168.0.10:50200", UiPresentationHelper.NormalizeBrowserUrl(" 192.168.0.10:50200 "));
        }

        private static void TestPreservesSupportedSchemes()
        {
            AssertEqual("http://10.12.35.82:30008/", UiPresentationHelper.NormalizeBrowserUrl("http://10.12.35.82:30008/"));
            AssertEqual("https://cliente.local/protheus", UiPresentationHelper.NormalizeBrowserUrl("https://cliente.local/protheus"));
        }

        private static void TestRejectsUnsupportedSchemes()
        {
            AssertEqual(null, UiPresentationHelper.NormalizeBrowserUrl("ftp://cliente.local"));
            AssertEqual(null, UiPresentationHelper.NormalizeBrowserUrl("javascript:alert(1)"));
            AssertEqual(null, UiPresentationHelper.NormalizeBrowserUrl("   "));
        }

        private static void TestClientNameMatchReturnsAllEnvironments()
        {
            ClientItem client = CreateClient();
            List<EnvironmentItem> result = UiPresentationHelper.GetVisibleEnvironments(client, "MAPRIAL");
            AssertEqual(2, result.Count);
        }

        private static void TestFiltersByEnvironmentName()
        {
            ClientItem client = CreateClient();
            List<EnvironmentItem> result = UiPresentationHelper.GetVisibleEnvironments(client, "desARROLLo");
            AssertEqual(1, result.Count);
            AssertEqual("Desarrollo", result[0].Name);
        }

        private static void TestFiltersByPartialIp()
        {
            ClientItem client = CreateClient();
            List<EnvironmentItem> result = UiPresentationHelper.GetVisibleEnvironments(client, "50101");
            AssertEqual(1, result.Count);
            AssertEqual("Desarrollo", result[0].Name);
        }

        private static void TestMatchesClientOrEnvironment()
        {
            ClientItem client = CreateClient();
            AssertEqual(true, UiPresentationHelper.ClientMatchesSearch(client, "maprial"));
            AssertEqual(true, UiPresentationHelper.ClientMatchesSearch(client, "desarrollo"));
            AssertEqual(true, UiPresentationHelper.ClientMatchesSearch(client, "50200"));
            AssertEqual(false, UiPresentationHelper.ClientMatchesSearch(client, "lafage"));
        }

        private static void TestFilteringDoesNotMutateClient()
        {
            ClientItem client = CreateClient();
            List<EnvironmentItem> result = UiPresentationHelper.GetVisibleEnvironments(client, "no-existe");
            AssertEqual(0, result.Count);
            AssertEqual(2, client.Environments.Count);
        }

        private static void TestSearchTemporarilyExpandsClients()
        {
            AssertEqual(false, UiPresentationHelper.ShouldShowEnvironments(true, ""));
            AssertEqual(true, UiPresentationHelper.ShouldShowEnvironments(false, ""));
            AssertEqual(true, UiPresentationHelper.ShouldShowEnvironments(true, "produccion"));
        }

        private static void TestParseServerAddress()
        {
            var p1 = UiPresentationHelper.ParseServerAddress("https://10.12.35.82:30008/webapp/");
            AssertEqual(true, p1.IsHttps);
            AssertEqual("10.12.35.82", p1.Host);
            AssertEqual("30008", p1.WebPort);

            var p2 = UiPresentationHelper.ParseServerAddress("http://10.12.35.82:30008/");
            AssertEqual(false, p2.IsHttps);
            AssertEqual("10.12.35.82", p2.Host);
            AssertEqual("30008", p2.WebPort);

            var p3 = UiPresentationHelper.ParseServerAddress("192.168.0.10:50200");
            AssertEqual(false, p3.IsHttps);
            AssertEqual("192.168.0.10", p3.Host);
            AssertEqual("50200", p3.WebPort);

            var p4 = UiPresentationHelper.ParseServerAddress("erp.empresa.com");
            AssertEqual(false, p4.IsHttps);
            AssertEqual("erp.empresa.com", p4.Host);
            AssertEqual("", p4.WebPort);
        }

        private static void TestBuildServerAddress()
        {
            AssertEqual("https://10.12.35.82:30008/webapp/", UiPresentationHelper.BuildServerAddress(true, "10.12.35.82", "30008"));
            AssertEqual("http://10.12.35.82:30008/webapp/", UiPresentationHelper.BuildServerAddress(false, "10.12.35.82", "30008"));
            AssertEqual("https://192.168.0.10:50200/webapp/", UiPresentationHelper.BuildServerAddress(true, "http://192.168.0.10", "50200"));
            AssertEqual("http://erp.empresa.com:8080/webapp/", UiPresentationHelper.BuildServerAddress(false, "erp.empresa.com/", "8080"));
        }

        private static ClientItem CreateClient()
        {
            return new ClientItem
            {
                Name = "Maprial",
                Environments = new List<EnvironmentItem>
                {
                    new EnvironmentItem { Name = "Produccion", ServerIp = "192.168.0.10:50200" },
                    new EnvironmentItem { Name = "Desarrollo", ServerIp = "192.168.0.10:50101" }
                }
            };
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS: " + name);
            }
            catch (Exception ex)
            {
                failures++;
                Console.Error.WriteLine("FAIL: " + name + " -> " + ex.Message);
            }
        }

        private static void AssertEqual<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException("esperado <" + expected + ">, obtenido <" + actual + ">");
            }
        }
    }
}
