﻿using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Uniconta.ClientTools.DataModel;
using Uniconta.Common;
using Uniconta.DataModel;
using ZendoImporter.Core.Helpers;
using ZendoImporter.Core.Managers;

namespace ZendoImporter.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.CB_Company.ItemsSource = UnicontaAPIManager.GetCompanies();
            this.CB_Company.SelectedItem = UnicontaAPIManager.GetCurrentCompany();
            this.CB_Company.SelectionChanged += CB_Company_SelectionChanged;
        }
        
        #region Event Methods
        private async void CB_Company_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Setting Company
            var company = this.CB_Company.SelectedItem as Company;
            await UnicontaAPIManager.SetCurrentCompany(company);
        }
        #endregion

        private async void B_Customers_Click(object sender, RoutedEventArgs e)
        {
            // Getting CrudAPI
            var crudAPI = UnicontaAPIManager.GetCrudAPI();
            crudAPI.RunInTransaction = false;

            // Parsing CSV
            var customers = CSVUtils.ParseCustomers(@"C:\src\Uniconta\Technical-Training-Cases-master\TrainingData\CompanyData\Finace-Customers.csv");

            // Creating Insert List
            var newDebtorClients = new List<DebtorClient>();
            foreach (var customer in customers)
            {
                // Parsing Account Number
                var accountNumber = (int.Parse(customer.AccountNumber) + 20000).ToString();

                newDebtorClients.Add(new DebtorClient
                {
                    _Account = accountNumber.ToString(),
                    _Name = customer.AccountName,
                    _Address1 = customer.Address1,
                    _Address2 = customer.Address2,
                    _ZipCode = customer.ZIP,
                    _Phone = customer.Telephone
                });
            };
            
            // Calling insert API
            var errorCode = await crudAPI.Insert(newDebtorClients);
            if (errorCode != ErrorCodes.Succes)
                MessageBox.Show($"ERROR: Failed to import customers {errorCode.ToString()}");
            else
                MessageBox.Show("Import Completed");
        }

        private async void B_Items_Click(object sender, RoutedEventArgs e)
        {
            // Getting CrudAPI
            var crudAPI = UnicontaAPIManager.GetCrudAPI();
            crudAPI.RunInTransaction = false;

            // Parsing CSV
            var items = CSVUtils.ParseItems(@"C:\src\Uniconta\Technical-Training-Cases-master\TrainingData\CompanyData\Finace-Items.csv");
            
            // Creating Insert List
            var newInvItemClients = new List<InvItemClient>();
            foreach(var item in items)
            {
                newInvItemClients.Add(new InvItemClient
                {
                    _Item = item.Item,
                    _Name = item.ItemName,
                    _SalesPrice1 = item.SalesPrice,
                    _Group = "Grp1"
                });
            };

            // Calling insert API
            var errorCode = await crudAPI.Insert(newInvItemClients);
            if (errorCode != ErrorCodes.Succes)
                MessageBox.Show($"ERROR: Failed to import items {errorCode.ToString()}");
            else
                MessageBox.Show("Import Completed");
        }

        private async void B_Orders_Click(object sender, RoutedEventArgs e)
        {
            // Getting CrudAPI
            var crudAPI = UnicontaAPIManager.GetCrudAPI();
            crudAPI.RunInTransaction = false;

            // Parsing CSV
            var orders = CSVUtils.ParseOrders(@"C:\src\Uniconta\Technical-Training-Cases-master\TrainingData\CompanyData\Finace-Orders.csv");

            // Creating SQLCache's
            SQLCache customerCache = crudAPI.CompanyEntity.GetCache(typeof(DebtorClient));
            if (customerCache == null)
                customerCache = await crudAPI.CompanyEntity.LoadCache(typeof(DebtorClient), crudAPI);

            SQLCache inventoryCache = crudAPI.CompanyEntity.GetCache(typeof(InvItemClient));
            if (inventoryCache == null)
                inventoryCache = await crudAPI.CompanyEntity.LoadCache(typeof(InvItemClient), crudAPI);
            
            // Creating Insert List
            var newDebtorOrderClients = new List<DebtorOrderClient>();
            foreach(var order in orders)
            {
                // Parsing Account Number
                var accountNumber = (int.Parse(order.AccountNumber) + 20000).ToString();

                // Finding customer in cache
                var customer = customerCache.Get(accountNumber) as DebtorClient;

                var newDebtorOrderClient = new DebtorOrderClient
                {
                    _Created = order.CreatedDate
                };
                newDebtorOrderClient.SetMaster(customer);
                newDebtorOrderClients.Add(newDebtorOrderClient);
            };
            
            // Calling insert API
            var errorCode = await crudAPI.Insert(newDebtorOrderClients);
            if (errorCode != ErrorCodes.Succes)
                MessageBox.Show($"ERROR: Failed to import orders {errorCode.ToString()}");

            // Creating order lines
            var newDebtorOrderLineClients = new List<DebtorOrderLineClient>();
            var inventoryList = inventoryCache.GetRecords as InvItemClient[];
            
            var index = 0;
            foreach (var debtorOrder in newDebtorOrderClients)
            {
                var orderItems = orders[index].Items;
                foreach(var item in orderItems)
                {
                    var inventoryItem = inventoryList.FirstOrDefault(i => i.Name == item.ItemName);

                    var orderLine = new DebtorOrderLineClient
                    {
                        _Item = inventoryItem.Item,
                        _Qty = 1,
                        _Price = inventoryItem.SalesPrice1
                    };
                    orderLine.SetMaster(debtorOrder);
                    newDebtorOrderLineClients.Add(orderLine);
                };

                index++;
            };
            
            // Calling insert API
            var errorCode2 = await crudAPI.Insert(newDebtorOrderLineClients);
            if (errorCode2 != ErrorCodes.Succes)
                MessageBox.Show($"ERROR: Failed to import order lines {errorCode2.ToString()}");
            else
                MessageBox.Show("Import Completed");
        }
    }
}
