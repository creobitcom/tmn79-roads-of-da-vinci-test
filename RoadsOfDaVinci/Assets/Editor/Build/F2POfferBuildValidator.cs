using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Creobit.EditionsUpgrade;
using UnityEditor;
using UnityEngine;

namespace Creobit.CI
{
    /// <summary>
    /// Build-time gate for the F2P purchase flow. It protects both parts of the App Store contract:
    /// all four products must be present in the catalog and every rotating offer must be reachable
    /// from the UnlockLevelWindow prefab with the correct purchase button.
    /// </summary>
    public static class F2POfferBuildValidator
    {
        private const string CatalogPath = "Assets/Resources/IAPProductCatalog.json";
        private const string UnlockWindowPath =
            "Assets/Creobit/EditionsUpgrade/Prefabs/Offers/UnlockLevelWindow.prefab";

        private static readonly string[] ProductSuffixes =
        {
            "fullprice",
            "start",
            "season",
            "oneminute"
        };

        public static void Validate(string edition, string bundleId)
        {
            if (edition.IndexOf("f2p", StringComparison.OrdinalIgnoreCase) < 0)
                return;

#if PREMIUM
            throw new InvalidOperationException(
                $"[CiBuild] F2P offer gate: edition '{edition}' is being validated by an editor " +
                "compiled with PREMIUM. Run PrepareEdition before Build so F2P purchase scripts are available.");
#else
            if (string.IsNullOrWhiteSpace(bundleId))
                throw new InvalidOperationException("[CiBuild] F2P offer gate: applicationIdentifier is empty.");

            var expectedProducts = ProductSuffixes
                .Select(suffix => $"{bundleId}.{suffix}")
                .ToArray();

            ValidateSchedule();
            ValidateCatalog(expectedProducts);
            ValidateUnlockWindow(expectedProducts);

            Debug.Log(
                $"[CiBuild] F2P offer gate: PASS — Start/Season/One-minute rotation is reachable; " +
                $"catalog and prefab expose exactly: {string.Join(", ", expectedProducts)}.");
#endif
        }

        public static void ValidateFromCli()
        {
            try
            {
                Validate("appstore_macos_f2p", PlayerSettings.applicationIdentifier);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                throw;
            }
        }

#if !PREMIUM
        private static void ValidateSchedule()
        {
            const int firstWindow = 7;
            var expected = new Dictionary<int, UnlockLevelWindow.OfferKind>
            {
                { firstWindow, UnlockLevelWindow.OfferKind.Start },
                { firstWindow + 1, UnlockLevelWindow.OfferKind.Season },
                { firstWindow + 2, UnlockLevelWindow.OfferKind.Season },
                { firstWindow + 3, UnlockLevelWindow.OfferKind.Season },
                { firstWindow + 4, UnlockLevelWindow.OfferKind.OneMinute },
                { firstWindow + 8, UnlockLevelWindow.OfferKind.OneMinute },
                { firstWindow + 11, UnlockLevelWindow.OfferKind.Season },
                { firstWindow + 15, UnlockLevelWindow.OfferKind.OneMinute }
            };

            foreach (var pair in expected)
            {
                var actual = UnlockLevelWindow.GetScheduledOffer(firstWindow, pair.Key);
                if (actual != pair.Value)
                {
                    throw new InvalidOperationException(
                        $"[CiBuild] F2P offer gate: level/window {pair.Key} must show {pair.Value}, " +
                        $"but the rotation returns {actual}.");
                }
            }
        }

        private static void ValidateCatalog(IReadOnlyCollection<string> expectedProducts)
        {
            if (!File.Exists(CatalogPath))
                throw new FileNotFoundException("[CiBuild] F2P offer gate: IAP catalog not found.", CatalogPath);

            var catalog = JsonUtility.FromJson<ProductCatalog>(File.ReadAllText(CatalogPath));
            if (catalog?.products == null)
                throw new InvalidOperationException("[CiBuild] F2P offer gate: IAP catalog has no products array.");

            var products = catalog.products.Where(product => product != null).ToArray();
            var actualIds = products.Select(product => product.id).ToArray();
            AssertExactProductSet("IAP catalog", expectedProducts, actualIds);

            var wrongTypes = products
                .Where(product => expectedProducts.Contains(product.id) && product.type != 1)
                .Select(product => $"{product.id} (type={product.type})")
                .ToArray();
            if (wrongTypes.Length > 0)
            {
                throw new InvalidOperationException(
                    "[CiBuild] F2P offer gate: all offer products must be Non-Consumable (type=1): " +
                    string.Join(", ", wrongTypes));
            }
        }

        private static void ValidateUnlockWindow(IReadOnlyCollection<string> expectedProducts)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnlockWindowPath);
            if (prefab == null)
                throw new InvalidOperationException($"[CiBuild] F2P offer gate: prefab not found: {UnlockWindowPath}");

            var window = prefab.GetComponentInChildren<UnlockLevelWindow>(true);
            if (window == null)
                throw new InvalidOperationException("[CiBuild] F2P offer gate: UnlockLevelWindow component not found.");

            var serializedWindow = new SerializedObject(window);
            ValidateOfferArray(serializedWindow, "_startObjects", expectedProducts.Single(id => id.EndsWith(".start")));
            ValidateOfferArray(serializedWindow, "_sessionObjects", expectedProducts.Single(id => id.EndsWith(".season")));
            ValidateOfferArray(serializedWindow, "_oneMinuteObjects", expectedProducts.Single(id => id.EndsWith(".oneminute")));

            var prefabProductIds = prefab.GetComponentsInChildren<CreobitIAPButton>(true)
                .Where(button => button.IsAPurchaseButton())
                .Select(button => button.GetProductId())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToArray();
            AssertExactProductSet("UnlockLevelWindow prefab", expectedProducts, prefabProductIds);
        }

        private static void ValidateOfferArray(SerializedObject serializedWindow, string propertyName,
            string expectedProductId)
        {
            var property = serializedWindow.FindProperty(propertyName);
            if (property == null || !property.isArray || property.arraySize < 3)
            {
                throw new InvalidOperationException(
                    $"[CiBuild] F2P offer gate: {propertyName} must contain at least three objects " +
                    "(the purchase button is element 2).");
            }

            var buttonObject = property.GetArrayElementAtIndex(2).objectReferenceValue as GameObject;
            var button = buttonObject == null ? null : buttonObject.GetComponent<CreobitIAPButton>();
            if (button == null)
            {
                throw new InvalidOperationException(
                    $"[CiBuild] F2P offer gate: {propertyName}[2] has no CreobitIAPButton.");
            }

            var actualProductId = button.GetProductId();
            if (!string.Equals(actualProductId, expectedProductId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"[CiBuild] F2P offer gate: {propertyName}[2] must buy '{expectedProductId}', " +
                    $"but buys '{actualProductId}'.");
            }
        }

        private static void AssertExactProductSet(string source, IReadOnlyCollection<string> expected,
            IEnumerable<string> actual)
        {
            var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
            var actualList = actual.ToArray();
            var actualSet = new HashSet<string>(actualList, StringComparer.Ordinal);
            var missing = expectedSet.Except(actualSet).ToArray();
            var extra = actualSet.Except(expectedSet).ToArray();
            var duplicates = actualList.GroupBy(id => id, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            if (missing.Length == 0 && extra.Length == 0 && duplicates.Length == 0)
                return;

            throw new InvalidOperationException(
                $"[CiBuild] F2P offer gate: {source} does not expose exactly the four expected products. " +
                $"Missing=[{string.Join(", ", missing)}], extra=[{string.Join(", ", extra)}], " +
                $"duplicates=[{string.Join(", ", duplicates)}].");
        }

        [Serializable]
        private sealed class ProductCatalog
        {
            public Product[] products;
        }

        [Serializable]
        private sealed class Product
        {
            public string id;
            public int type;
        }
#endif
    }
}
