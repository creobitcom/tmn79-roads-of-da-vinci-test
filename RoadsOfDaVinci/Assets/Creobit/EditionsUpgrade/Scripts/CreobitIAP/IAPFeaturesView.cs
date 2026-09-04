#if !PREMIUM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Creobit.EditionsUpgrade;
using TMPro;
using UnityEngine;
using UnityEngine.Purchasing;

public class IAPFeaturesView : MonoBehaviour
{
    [Serializable]
    public class IAPFeature
    {
        [SerializeField] public TMP_Text PriceText;
        [SerializeField, HideInInspector] public decimal priceNumber;
        [field: SerializeField, Range(0, 100)] public float Percent { get; private set; } = 100;
        [SerializeField] public bool IsMainSet;

        public void SetPercent(float percent)
        {
            Percent = percent;
        }
    }
    
    [field: SerializeField] public List<IAPFeature> IapFeatures { get; private set; } = new List<IAPFeature>();
    [SerializeField] private CreobitIAPButton _iapButton;

    [HideInInspector] public bool Calculated = false;

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }
        
        if (IapFeatures.Count <= 0)
        {
            return;
        }

        var mainSetsCount = IapFeatures.Count(x => x.IsMainSet); 
        if (mainSetsCount != 1)
        {
            FixMainSet(mainSetsCount);
        }
        
        CheckPercents();
    }

    private void CheckPercents()
    {
        var startCount = IapFeatures.Count;
        
        if (Application.isPlaying)
        {
            for (int i = 0; i < IapFeatures.Count; i++)
            {
                if (IapFeatures[i].PriceText == null || !IapFeatures[i].PriceText.gameObject.activeInHierarchy)
                {
                    IapFeatures.Remove(IapFeatures[i]);
                    i--;
                }
            }
        }

        var endCount = IapFeatures.Count;

        if (endCount!=startCount)
        {
            switch (IapFeatures.Count)
            {
                case 1:
                    IapFeatures.First().SetPercent(100);
                    break;
                case 2:
                    IapFeatures.Last().SetPercent(30);
                    break;
                case 3:
                    foreach (var feature in IapFeatures.Where(x=>!x.IsMainSet))
                    {
                        feature.SetPercent(25);
                    }
                    break;
                case 4:
                    foreach (var feature in IapFeatures.Where(x=>!x.IsMainSet))
                    {
                        feature.SetPercent(20);
                    }
                    break;
                default:
                    Debug.LogError("Unexpected length!");
                    break;
            }
        }
        
        var totalPercent = IapFeatures.Sum(iapFeature => iapFeature.Percent);

        if (Math.Abs(totalPercent - 100) > 0)
        {
            FixPercent(totalPercent);
        }        
    }
    
    private void FixMainSet(int count)
    {
        if (count < 1)
        {
            if (IapFeatures.Count > 0)
            {
                IapFeatures.First().IsMainSet = true;
            }
        }
        else
        {
            foreach (var iapFeature in IapFeatures)
            {
                iapFeature.IsMainSet = false;
            }
            IapFeatures.First().IsMainSet = true;
        }
    }
    
    private void FixPercent(float totalPercent)
    {
        var mainSet = GetMainSet();
        if (mainSet == null)
        {
            return;
        }
        
        var difference = 0f;
        var tempPercent = 0f;
        
        if (totalPercent > 100)
        {
            difference = totalPercent - 100;
            tempPercent = mainSet.Percent - difference;
        }
        else if (totalPercent < 100)
        {
            difference = 100 - totalPercent;
            tempPercent = mainSet.Percent + difference;
        }
        
        mainSet.SetPercent(tempPercent);
    }
    
    private IAPFeature GetMainSet()
    {
        IAPFeature mainSet = null;
        if (IapFeatures.Count<=0)
        {
            return null;
        }
        
        if (IapFeatures.Any(x => x.IsMainSet))
        {
            mainSet = IapFeatures.FirstOrDefault(x => x.IsMainSet);
        }
        else
        {
            mainSet = IapFeatures.First();
            mainSet.IsMainSet = true;
        }
        return mainSet;
    }

    private bool productWaitingEnabled = false;

    private void OnEnable()
    {
        foreach (var iapFeature in IapFeatures)
        {
            iapFeature.priceNumber = 0;
        }

        if (_iapButton.FetchedProduct!=null)
        {
            CalculatePrice();
        }
        else
        {
            productWaitingEnabled = true;
        }
    }

    private void Start()
    {
        CheckPercents();
        if (_iapButton.FetchedProduct!=null)
        {
            CalculatePrice();
        }
        else
        {
            productWaitingEnabled = true;
        }
    }

    private void Update()
    {
        if (!productWaitingEnabled || _iapButton.FetchedProduct == null)
        {
            return;
        }
        productWaitingEnabled = false;
        CalculatePrice();
    }

    [ContextMenu("Calculate Price")]
    private void CalculatePrice()
    {
        var product = _iapButton.FetchedProduct;
        var fullPrice = product.metadata.localizedPrice;
        var priceSymbol = Regex.Replace(product.metadata.localizedPriceString, @"[\d\s.,;:!?]", "");

        var remainder = 0m;
        var featuresTotalPrice = 0m;
        
        if (fullPrice <= 99)
        {
            if (IapFeatures.Count > 1)
            {
                var selectedFeatures = IapFeatures.Where(x => !x.IsMainSet).ToList();
                
                for (int i = 0; i < selectedFeatures.Count; i++)
                {
                    var price = product.metadata.localizedPriceString;
                    selectedFeatures[i].priceNumber = product.metadata.localizedPrice;

                    if (price.StartsWith(priceSymbol))
                    {
                        price = priceSymbol + " {0}";
                    }
                    else
                    {
                        price = "{0} " + priceSymbol;
                    }
                    
                    selectedFeatures[i].priceNumber *= (decimal)selectedFeatures[i].Percent / 100;
                    
                    var tokens = selectedFeatures[i].priceNumber.ToString("G", CultureInfo.InvariantCulture).Split(".");
                    var length = tokens.Length > 1 ? tokens[1].Length : 0;

                    if (length > 0)
                    {
                        selectedFeatures[i].priceNumber = Math.Round(selectedFeatures[i].priceNumber, 2);
                        var priceReminder = selectedFeatures[i].priceNumber % 1;
                        
                        if (!priceReminder.ToString().EndsWith("9"))
                        {
                            var lastNumber = Convert.ToDecimal(priceReminder.ToString().Last().ToString());
                            
                            lastNumber = priceReminder.ToString().Length <= 3 ? 0m : Convert.ToDecimal(priceReminder.ToString().Last().ToString());

                            
                            if (lastNumber >= 5)
                            {
                                var difference = Math.Abs(lastNumber - 9m);
                                var priceNumString = selectedFeatures[i].priceNumber.ToString();
                                priceNumString = priceNumString.Remove(priceNumString.Length - 1) + "9";
                                selectedFeatures[i].priceNumber = Convert.ToDecimal(priceNumString);
                                remainder += difference / 100;
                            }
                            else
                            {
                                var difference = lastNumber + 1m;
                                selectedFeatures[i].priceNumber -=difference/100;
                                remainder -= difference / 100;
                            }
                        }
                    }

                    featuresTotalPrice += selectedFeatures[i].priceNumber;
                    
                    price = String.Format(price,selectedFeatures[i].priceNumber);
                    selectedFeatures[i].PriceText.text = price;
                }
            }

            var mainSet = IapFeatures.First(x => x.IsMainSet);
            var mainPrice = product.metadata.localizedPriceString;
            mainSet.priceNumber = product.metadata.localizedPrice;

            Calculated = true;
                    
            if (mainPrice.StartsWith(priceSymbol))
            {
                mainPrice = priceSymbol + " {0}";
            }
            else
            {
                mainPrice = "{0} " + priceSymbol;
            }
                    
            mainSet.priceNumber *= (decimal)mainSet.Percent / 100;
            mainSet.priceNumber -= remainder;
            mainSet.priceNumber = Math.Round(mainSet.priceNumber, 2);
            featuresTotalPrice += mainSet.priceNumber;
            if (featuresTotalPrice != fullPrice)
            {
                var diff = featuresTotalPrice - fullPrice;
                mainSet.priceNumber -= diff;
            }
            
            mainPrice = String.Format(mainPrice,mainSet.priceNumber);
            mainSet.PriceText.text = mainPrice;
        }
        else
        {
            var selectedFeatures = IapFeatures.Where(x => !x.IsMainSet).ToList();
                
            for (int i = 0; i < selectedFeatures.Count; i++)
            {
                var price = product.metadata.localizedPriceString;
                selectedFeatures[i].priceNumber = product.metadata.localizedPrice;

                if (price.StartsWith(priceSymbol))
                {
                    price = priceSymbol + " {0}";
                }
                else
                {
                    price = "{0} " + priceSymbol;
                }
                
                selectedFeatures[i].priceNumber *= (decimal)selectedFeatures[i].Percent / 100;
                
                var tokens = selectedFeatures[i].priceNumber.ToString("G", CultureInfo.InvariantCulture).Split(".");
                var length = tokens.Length > 1 ? tokens[1].Length : 0;

                if (length > 0)
                {
                    selectedFeatures[i].priceNumber = Math.Round(selectedFeatures[i].priceNumber, 0);

                    var priceReminder = selectedFeatures[i].priceNumber;
                    
                    if (!priceReminder.ToString().EndsWith("9"))
                    {
                        var lastNumber = Convert.ToDecimal(priceReminder.ToString().Last().ToString());

                        if (lastNumber >= 5)
                        {
                            var difference = Math.Abs(lastNumber - 9m);
                            selectedFeatures[i].priceNumber += difference;
                            remainder += difference;
                        }
                        else
                        {
                            var difference = lastNumber + 1m;
                            var priceNumString = selectedFeatures[i].priceNumber.ToString();
                            priceNumString = priceNumString.Remove(priceNumString.Length - 1) + difference.ToString();
                            selectedFeatures[i].priceNumber = Convert.ToDecimal(priceNumString);
                            remainder -= difference;
                        }
                    }
                }

                featuresTotalPrice += selectedFeatures[i].priceNumber;
                
                price = String.Format(price,selectedFeatures[i].priceNumber);
                selectedFeatures[i].PriceText.text = price;
            }

            var mainSet = IapFeatures.First(x => x.IsMainSet);
            var mainPrice = product.metadata.localizedPriceString;
            mainSet.priceNumber = product.metadata.localizedPrice;

            Calculated = true;
                    
            if (mainPrice.StartsWith(priceSymbol))
            {
                mainPrice = priceSymbol + " {0}";
            }
            else
            {
                mainPrice = "{0} " + priceSymbol;
            }
                    
            mainSet.priceNumber *= (decimal)mainSet.Percent / 100;
            mainSet.priceNumber -= remainder;
            mainSet.priceNumber = Math.Round(mainSet.priceNumber, 0);
            featuresTotalPrice += mainSet.priceNumber;

            if (featuresTotalPrice != fullPrice)
            {
                var diff = featuresTotalPrice - fullPrice;
                mainSet.priceNumber -= diff;
            }
            
            mainPrice = String.Format(mainPrice,mainSet.priceNumber);
            mainSet.PriceText.text = mainPrice;
        }
    }
}
#endif