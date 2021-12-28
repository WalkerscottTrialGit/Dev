using System;
using Microsoft.Xrm.Sdk;
using EarlyBound;
using Microsoft.Xrm.Sdk.Query;

namespace D365.Walkerscott
{
    public class OrderCalculations : PluginBase
    {
        #region Constructor/Configuration
        private string _secureConfig = null;
        private string _unsecureConfig = null;
        private readonly string postImageAlias = "PostImage";
        public OrderCalculations(string unsecure, string secureConfig)
            : base(typeof(OrderCalculations))
        {
            _secureConfig = secureConfig;
            _unsecureConfig = unsecure;
        }
        #endregion

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)

        {
            if (localContext == null)
                throw new ArgumentNullException(nameof(localContext));

            // TODO: Implement your custom code
            IPluginExecutionContext context = localContext.PluginExecutionContext;
            IOrganizationService orgSvc = localContext.OrganizationService;
            ITracingService tracingSvc = localContext.TracingService;
            try
            {
                if (context.Depth > 1)
                {
                    tracingSvc.Trace("context.Depth on return : {0}", context.Depth);
                    return;
                }

                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
                {                   
                    var target = context.InputParameters["Target"] as Entity;
                    var orderLine = target.ToEntity<ws_OrderLine>();
                  
                    Guid orderLineId = orderLine.Id;
                    decimal discount = 0;
                    decimal subTotal = 0;
                    decimal totalDiscount = 0;

                    if (context.MessageName == "Create")
                    {

                        tracingSvc.Trace("Create logic");
                        if (orderLine!=null)
                            //(orderLine.Contains("ws_price") && orderLine.Contains("ws_quantity")))
                        {
                            

                            subTotal = ((Money)orderLine.ws_Price).Value * (decimal)orderLine.ws_Quantity;
                                //((Money)orderLine["ws_price"]).Value * ((decimal)orderLine["ws_quantity"]);
                            orderLine.ws_Subtotal = new Money(subTotal);

                            // Calculate discount based on the total amount
                            discount = CalculateDiscount(subTotal);
                            if (discount > 0)
                            {
                                orderLine.ws_DiscountPercentage = discount;
                                //orderLine["ws_discountpercentage"] = discount;

                                totalDiscount = subTotal * (discount / 100);
                                orderLine.ws_TotalDiscount = new Money(totalDiscount);
                                //orderLine["ws_totaldiscount"] = new Money(totalDiscount);

                                orderLine.ws_Total = new Money(subTotal - totalDiscount);
                                //orderLine["ws_total"] = new Money(subTotal - totalDiscount);

                                orgSvc.Update(orderLine);
                                //orderLine.ws_OrderHeaderId

                                //((Microsoft.Xrm.Sdk.EntityReference)(orderLine.ws_OrderHeaderId)).Id
                                if (orderLine.Attributes.Contains("ws_orderheaderid"))
                                UpdateOrderHeader(orgSvc,(orderLine.Attributes["ws_orderheaderid"] as EntityReference).Id, tracingSvc, null);
                            }
                        }

                    }

                    if (context.MessageName == "Update")
                    {
                        tracingSvc.Trace("Update logic");
                        Entity orderLinePostImage = context.PostEntityImages != null && context.PostEntityImages.Contains(this.postImageAlias) ? context.PostEntityImages[this.postImageAlias] : null;

                        if (orderLine != null && (orderLinePostImage.Contains("ws_price") || orderLinePostImage.Contains("ws_quantity")))
                        {
                           
                            subTotal = ((Money)orderLinePostImage.Attributes["ws_price"]).Value * ((decimal)orderLinePostImage.Attributes["ws_quantity"]);
                            orderLine["ws_subtotal"] = new Money(subTotal);

                            // Calculate discount based on the total amount
                            discount = CalculateDiscount(subTotal);
                            if (discount > 0)
                            {
                                orderLine.ws_DiscountPercentage = discount;
                                //orderLine["ws_discountpercentage"] = discount;

                                totalDiscount = subTotal * (discount / 100);
                                orderLine.ws_TotalDiscount = new Money(totalDiscount);
                                //orderLine["ws_totaldiscount"] = new Money(totalDiscount);

                                orderLine.ws_Total = new Money(subTotal - totalDiscount);
                                //orderLine["ws_total"] = new Money(subTotal - totalDiscount);

                                orgSvc.Update(orderLine);
                                if (orderLinePostImage.Contains("ws_orderheaderid"))
                                    UpdateOrderHeader(orgSvc, (orderLinePostImage.Attributes["ws_orderheaderid"] as EntityReference).Id, tracingSvc, null);
                                    //((Microsoft.Xrm.Sdk.EntityReference)(orderLinePostImage.Attributes["ws_orderheaderid"])).Id, tracingSvc, null);

                            }
                            //orderLine["ws_discountpercentage"] = discount;

                            //totalDiscount = subTotal * (discount / 100);
                            //orderLine["ws_totaldiscount"] = new Money(totalDiscount);

                            //orderLine["ws_total"] = new Money(subTotal - totalDiscount);

                            //orgSvc.Update(orderLine);
                            //UpdateOrderHeader(orgSvc, orderLineId, orderLinePostImage);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingSvc.Trace("DEBUG: Exception Message:{0}. InnerException:{1}", new object[] { ex.Message, ex.InnerException });
                throw;
            }
        }

        // Method to calculate discount.
        private static decimal CalculateDiscount(decimal amount)
        {
            decimal discount = 0;
            if (amount >= (decimal)1 && amount <= (decimal)500)
            {
                discount = 0.50M; //2%
            }
            else if (amount > (decimal)500.00 && amount <= (decimal)1000.00)
            {
                discount = 1; //2%
            }
            else if (amount > (decimal)1000.00 && amount <= (decimal)2000.00)
            {
                discount = 2; //2%
            }
            else if (amount > (decimal)2000.00 && amount <= (decimal)3000.00)
            {
                discount = 3; //3%
            }
            else if (amount > (decimal)3000.00 && amount <= (decimal)4000.00)
            {
                discount = 4; //3%
            }
            else if (amount > (decimal)5000.00 && amount <= (decimal)10000.00)
            {
                discount = 5;
            }
            else if (amount > (decimal)10000.00)
            {
                discount = 10;
            }
            return discount;
        }

        //
        public static void UpdateOrderHeader(IOrganizationService service, Guid orderHeaderGuid, ITracingService tracingSvc,Entity orderLinePostImage)
        {
            decimal orderSubtotal = 0;
            QueryExpression queryGetRequest = new QueryExpression("ws_orderline");
            queryGetRequest.ColumnSet = new ColumnSet("ws_subtotal","ws_total");
            queryGetRequest.Criteria.AddCondition("ws_orderheaderid", ConditionOperator.Equal,orderHeaderGuid);
            queryGetRequest.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            EntityCollection valueSumResult = service.RetrieveMultiple(queryGetRequest);
            foreach (var c in valueSumResult.Entities)
            {
                tracingSvc.Trace("UpdateOrderHeader contains data");
                if (c["ws_subtotal"] != null)
                {
                    orderSubtotal += (((Money)(c.Attributes["ws_subtotal"])).Value);
                }

            }
            //    ws_OrderHeader orderheaderObj = (ws_OrderHeader)service.Retrieve("ws_orderheader", orderHeaderGuid, cols);
            //Account retrievedAccount = (Account)_serviceProxy.Retrieve("account", _accountId, cols);
            ////Guid orderheaderObj = orderHeaderGuid;
            //orderheaderObj.ws_Subtotal = new Money(orderSubtotal);
             

            ws_OrderHeader orderheaderObj = new ws_OrderHeader
            {
                ws_Subtotal = new Money(orderSubtotal),
                ws_OrderHeaderId = orderHeaderGuid
            };
            service.Update(orderheaderObj);
        }

    }
}