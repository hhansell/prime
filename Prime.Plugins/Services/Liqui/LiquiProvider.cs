﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteDB;
using Prime.Common;
using Prime.Utility;

namespace Prime.Plugins.Services.Liqui
{
    /// <author email="scaruana_prime@outlook.com">Sean Caruana</author>
    // https://liqui.io/api
    public class LiquiProvider : IPublicPricingProvider, IAssetPairsProvider
    {
        private const string LiquiApiVersion = "3";
        private const string LiquiApiUrl = "https://api.liqui.io/api/" + LiquiApiVersion;

        private static readonly ObjectId IdHash = "prime:liqui".GetObjectIdHashCode();

        //From doc: All information is cached every 2 seconds, so there's no point in making more frequent requests.
        //https://liqui.io/api
        private static readonly IRateLimiter Limiter = new NoRateLimits();

        private RestApiClientProvider<ILiquiApi> ApiProvider { get; }

        public Network Network { get; } = Networks.I.Get("Liqui");

        public bool Disabled => false;
        public int Priority => 100;
        public string AggregatorName => null;
        public string Title => Network.Name;
        public ObjectId Id => IdHash;
        public IRateLimiter RateLimiter => Limiter;
        public bool IsDirect => true;
        public char? CommonPairSeparator => '_';

        public ApiConfiguration GetApiConfiguration => ApiConfiguration.Standard2;

        public LiquiProvider()
        {
            ApiProvider = new RestApiClientProvider<ILiquiApi>(LiquiApiUrl, this, (k) => null);
        }

        public async Task<bool> TestPublicApiAsync(NetworkProviderContext context)
        {
            var api = ApiProvider.GetApi(context);
            var r = await api.GetAssetPairsAsync().ConfigureAwait(false);

            return r?.pairs?.Count > 0;
        }

        public async Task<AssetPairs> GetAssetPairsAsync(NetworkProviderContext context)
        {
            var api = ApiProvider.GetApi(context);

            var r = await api.GetAssetPairsAsync().ConfigureAwait(false);

            if (r?.pairs == null || r.pairs.Count == 0)
            {
                throw new ApiResponseException("No asset pairs returned", this);
            }

            var pairs = new AssetPairs();

            foreach (var rCurrentTicker in r.pairs)
            {
                pairs.Add(rCurrentTicker.Key.ToAssetPair(this));
            }

            return pairs;
        }

        public IAssetCodeConverter GetAssetCodeConverter()
        {
            return null;
        }

        private static readonly PricingFeatures StaticPricingFeatures = new PricingFeatures()
        {
            Single = new PricingSingleFeatures() { CanStatistics = true, CanVolume = true },
            Bulk = new PricingBulkFeatures() { CanStatistics = true, CanVolume = true }
        };

        public PricingFeatures PricingFeatures => StaticPricingFeatures;

        public async Task<MarketPrices> GetPricingAsync(PublicPricesContext context)
        {
            if (context.ForSingleMethod)
                return await GetPriceAsync(context).ConfigureAwait(false);

            return await GetPricesAsync(context).ConfigureAwait(false);

        }

        public async Task<MarketPrices> GetPriceAsync(PublicPricesContext context)
        {
            var api = ApiProvider.GetApi(context);
            var pairCode = context.Pair.ToTicker(this).ToLower();
            var r = await api.GetTickerAsync(pairCode).ConfigureAwait(false);

            return new MarketPrices(new MarketPrice(Network, context.Pair, r.last)
            {
                PriceStatistics = new PriceStatistics(Network, context.Pair.Asset2, r.sell, r.buy, r.low, r.high),
                Volume = new NetworkPairVolume(Network, context.Pair, r.vol)
            });
        }

        public async Task<MarketPrices> GetPricesAsync(PublicPricesContext context)
        {
            var api = ApiProvider.GetApi(context);
            var pairsCsv = string.Join("-", context.Pairs.Select(x => x.ToTicker(this).ToLower()));
            var r = await api.GetTickersAsync(pairsCsv).ConfigureAwait(false);

            if (r == null || r.Count == 0)
            {
                throw new ApiResponseException("No tickers returned", this);
            }

            var prices = new MarketPrices();

            foreach (var pair in context.Pairs)
            {
                var currentTicker = r.FirstOrDefault(x => x.Key.ToAssetPair(this).Equals(pair)).Value;

                if (currentTicker == null)
                {
                    prices.MissedPairs.Add(pair);
                }
                else
                {
                    prices.Add(new MarketPrice(Network, pair, currentTicker.last)
                    {
                        PriceStatistics = new PriceStatistics(Network, pair.Asset2, currentTicker.sell, currentTicker.buy, currentTicker.low, currentTicker.high),
                        Volume = new NetworkPairVolume(Network, pair, currentTicker.vol)
                    });
                }
            }

            return prices;
        }
    }
}
