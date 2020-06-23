using System;
using System.Linq;
using System.Threading.Tasks;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class TSMCNewsScalper : Robot
    {
        [Parameter("News Release Hour")]
        public int Hour { get; set; }
        [Parameter("News Release Minute")]
        public int Minute { get; set; }
        [Parameter("News Release Second")]
        public int Seconds { get; set; }

        [Parameter("Lot Size", DefaultValue = 0.01)]
        public double Lots { get; set; }

        [Parameter("Stoploss Distance", DefaultValue = 12)]
        public double EntryDistance { get; set; }

        private bool placedorders = false;
        private DateTime NewsTime;
        private string BotID;
        private double pips;
        private DateTime canceltime;
        private bool opened;

        protected override void OnStart()
        {
            var now = DateTime.Now;
            NewsTime = new DateTime(now.Year, now.Month, now.Day, Hour, Minute, Seconds, 0);
            var tick = Symbol.PipSize;
            if (tick == 0.001 || tick == 1E-05)
                pips = tick * 10;
            else
                pips = tick;
            placedorders = false;
            BotID = DateTime.Now.ToLongDateString() + DateTime.Now.ToLongTimeString();
        }

        protected override void OnTick()
        {
            EnterTrades();
            CloseOrder();
            CancelOrders();
            breakEven();
        }

        protected override void OnBar()
        {
            TrailingStop();
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
        }

        private void EnterTrades()
        {
            if (!placedorders)
            {
                TimeSpan ts = NewsTime - DateTime.Now;
                bool state = ts.TotalSeconds <= 20 && ts.TotalSeconds >= 3;
                if (state)
                {
                    var buyresult = PlaceStopLimitOrder(TradeType.Buy, Symbol.Name, Symbol.QuantityToVolumeInUnits(Lots), Ask + (EntryDistance * pips), 3, BotID);
                    var sellresult = PlaceStopLimitOrder(TradeType.Sell, Symbol.Name, Symbol.QuantityToVolumeInUnits(Lots), Bid - (EntryDistance * pips), 3, BotID);
                    if (buyresult.IsSuccessful && sellresult.IsSuccessful)
                    {
                        placedorders = true;
                        buyresult.PendingOrder.ModifyStopLossPips(EntryDistance);
                        sellresult.PendingOrder.ModifyStopLossPips(EntryDistance);
                        canceltime = DateTime.Now.AddMinutes(1);
                        opened = true;
                        ;
                    }
                }
            }
        }

        private void CloseOrder()
        {
            if (Positions.Where(pos => pos.SymbolName == Symbol.Name && pos.Label == BotID).Count() == 1)
            {
                foreach (var order in PendingOrders.Where(pos => pos.SymbolName == Symbol.Name && pos.Label == BotID))
                    order.Cancel();
            }
        }

        private void CancelOrders()
        {
            if (DateTime.Now >= canceltime)
                if (PendingOrders.Where(pos => pos.SymbolName == Symbol.Name && pos.Label == BotID).Count() == 2)
                    foreach (var ord in PendingOrders.Where(pos => pos.SymbolName == Symbol.Name && pos.Label == BotID))
                        ord.Cancel();
        }

        //if (opened)
        //    {
        //        if (Ask > buypos.TargetPrice || Bid < sellpos.TargetPrice)
        //            foreach (var pos in PendingOrders.Where(pos => pos.SymbolName == Symbol.Name && pos.Label == BotID))
        //                pos.Cancel();
        //        //else if (DateTime.Now >= canceltime)

        //    }
        //}

        private void breakEven()
        {
            var pos = Positions.FirstOrDefault(p => p.SymbolName == Symbol.Name && p.Label == BotID);
            if (pos != null)
            {
                if (pos.TradeType == TradeType.Buy && Bid > pos.EntryPrice && (Bid - pos.EntryPrice) >= 3 * Symbol.PipSize && pos.StopLoss < pos.EntryPrice)
                    pos.ModifyStopLossPrice(pos.EntryPrice + (1 * Symbol.PipSize));
                else if (pos.TradeType == TradeType.Sell && Ask < pos.EntryPrice && (pos.EntryPrice - Ask) >= 3 * Symbol.PipSize && pos.StopLoss > pos.EntryPrice)
                    pos.ModifyStopLossPrice(pos.EntryPrice - (1 * Symbol.PipSize));
            }
        }

        private void TrailingStop()
        {
            var pos = Positions.FirstOrDefault(p => p.SymbolName == Symbol.Name && p.Label == BotID);
            if (pos != null)
            {
                if (pos.TradeType == TradeType.Buy && Bid > pos.StopLoss && pos.StopLoss > pos.EntryPrice)
                    if (Bars.Last(1).Close - (3 * Symbol.PipSize) > pos.StopLoss)
                        pos.ModifyStopLossPrice(Bars.Last(1).Close - (3 * Symbol.PipSize));

                if (pos.TradeType == TradeType.Sell && Ask < pos.StopLoss && pos.StopLoss < pos.EntryPrice)
                    if (Bars.Last(1).Close + (3 * Symbol.PipSize) < pos.StopLoss)
                        pos.ModifyStopLossPrice(Bars.Last(1).Close + (3 * Symbol.PipSize));
            }
        }
    }
}
