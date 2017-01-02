//----------------------------------------------------------------------------------
// Microsoft Developer & Platform Evangelism
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
//----------------------------------------------------------------------------------
// The example companies, organizations, products, domain names,
// e-mail addresses, logos, people, places, and events depicted
// herein are fictitious.  No association with any real company,
// organization, product, domain name, email address, logo, person,
// places, or events is intended or should be inferred.
//----------------------------------------------------------------------------------
namespace PopreceiptSample
{
    using Microsoft.Azure;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.WindowsAzure.Storage.Queue;
    using Microsoft.WindowsAzure.Storage.Table;
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.ProjectOxford.Face;
    /// <summary>
    /// Azure Queue Service Pop Receipt Sample - The Queue Service provides reliable messaging for workflow processing and for communication 
    /// between loosely coupled components of cloud services. This sample demonstrates how to perform tasks using the pop receipt value  
    /// returned by the service after an add message call.     
    /// 
    /// Note: This sample uses the .NET 4.5 asynchronous programming model to demonstrate how to call the Storage Service using the 
    /// storage client libraries asynchronous API's. When used in real applications this approach enables you to improve the 
    /// responsiveness of your application. Calls to the storage service are prefixed by the await keyword. 
    /// 
    /// Documentation References: 
    /// - What is a Storage Account - http://azure.microsoft.com/en-us/documentation/articles/storage-whatis-account/
    /// - Getting Started with Queues - http://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-queues/
    /// - Queue Service Concepts - http://msdn.microsoft.com/en-us/library/dd179353.aspx
    /// - Queue Service REST API - http://msdn.microsoft.com/en-us/library/dd179363.aspx
    /// - Queue Service C# API - http://go.microsoft.com/fwlink/?LinkID=398944
    /// - Storage Emulator - http://msdn.microsoft.com/en-us/library/azure/hh403989.aspx
    /// - Asynchronous Programming with Async and Await  - http://msdn.microsoft.com/en-us/library/hh191443.aspx
    /// </summary>
    public class Program
    {
        // *************************************************************************************************************************
        // Instructions: This sample can be run using either the Azure Storage Emulator that installs as part of the Azure SDK - or by
        // updating the App.Config file with your AccountName and Key. 
        // 
        // To run the sample using the Storage Emulator (default option)
        //      1. Start the Azure Storage Emulator (once only) by pressing the Start button or the Windows key and searching for it
        //         by typing "Azure Storage Emulator". Select it from the list of applications to start it.
        //      2. Create a folder named 'testfolder' and place a few photos
        //      3. Set breakpoints and run the project using F10. 
        // 
        // To run the sample using the Storage Service
        //      1. Open the app.config file and comment out the connection string for the emulator (UseDevelopmentStorage=True) and
        //         uncomment the connection string for the storage service (AccountName=[]...)
        //      2. Create a Storage Account through the Azure Portal and provide your [AccountName] and [AccountKey] in 
        //         the App.Config file. See http://go.microsoft.com/fwlink/?LinkId=325277 for more information
        //      3. Create a folder named 'testfolder' and place a few photos with people in them
        //      4. Set breakpoints and run the project using F10. 
        // 
        // *************************************************************************************************************************
        public static void Main(string[] args)
        {
            Console.WriteLine("Azure Storage Queue Sample\n");

            // Create or reference an existing queue, container and a table 
            CloudQueue queue = CreateQueueAsync().Result;
            CloudBlobContainer container = CreateBlobContainerAsync().Result;
            CloudTable table = CreateTableAsync().Result;

            // Face API Client
            FaceServiceClient faceClient = new FaceServiceClient(CloudConfigurationManager.GetSetting("FaceAPIKey"));

            // Process images to a blob account and save the estimated age as an entity in the table. Delete the messages in the queue using popreceipt
            processImage(queue, container, table, faceClient).Wait();

            // clean up the created queue, blobs and table
            cleanup(queue, container, table).Wait();

            Console.WriteLine("Press any key to exit");
            Console.Read();
        }

        /// <summary>
        /// Uploads all the images that reside in 'testfolder' and then adds
        /// the estimate age in a table after calling the Face API.
        /// Once successfully done, the message gets deleted from the queue
        /// Messages that remain the queue are the failed ones.
        /// </summary>
        /// <param name="queue">The sample queue</param>
        /// <param name="container">The sample blob container</param>
        /// <param name="table">The sample table</param>
        /// <param name="faceClient">Face API client</param>
        /// <returns></returns>
        public static async Task processImage(CloudQueue queue, CloudBlobContainer container, CloudTable table, FaceServiceClient faceClient)
        {

            Console.WriteLine("\n** Advanced sample starting...\n");

            try
            {

                // Iterate over photos in 'testfolder'
                var images = Directory.EnumerateFiles("testfolder", "*.jpg");

                foreach (string currentFile in images)
                {

                    string fileName = currentFile.Replace("testfolder\\", "");

                    Console.WriteLine("Processing image {0}", fileName);

                    // add a message to the queue for each photo
                    CloudQueueMessage message = new CloudQueueMessage(fileName);
                    queue.AddMessage(message, null, TimeSpan.FromSeconds(120));

                    // read the file
                    using (var fileStream = File.OpenRead(currentFile))
                    {

                        // detect face and estimate the age
                        var faces = await faceClient.DetectAsync(fileStream, false, true, new FaceAttributeType[] { FaceAttributeType.Age });
                        Console.WriteLine(" > " + faces.Length + " face(s) detected.");

                        CloudBlockBlob blob = container.GetBlockBlobReference(fileName);

                        var tableEntity = new DynamicTableEntity(DateTime.Now.ToString("yyMMdd"), fileName);

                        // iterate over detected faces
                        int i = 1;
                        foreach (var face in faces)
                        {

                            // Append the age info as property in the table entity
                            tableEntity.Properties.Add("person" + i.ToString(), new EntityProperty(face.FaceAttributes.Age.ToString()));
                            i++;

                        }

                        // upload the blob if a face was detected
                        if (faces.Length > 0)
                            await blob.UploadFromFileAsync(currentFile);

                        // store the age info in the table
                        table.Execute(TableOperation.InsertOrReplace(tableEntity));

                        // If blob and the entity exist, delete the queue message with the pop receipt
                        if (blob.Exists() && table.Execute(TableOperation.Retrieve(DateTime.Now.ToString("yyMMdd"), fileName)).HttpStatusCode == 200) { 
                            await queue.DeleteMessageAsync(message.Id, message.PopReceipt);
                        } else
                        {
                            await queue.UpdateMessageAsync(message, TimeSpan.FromSeconds(0), MessageUpdateFields.Visibility);
                        }

                    }

                }
            }
            catch (StorageException ex)
            {
                Console.WriteLine("Storage error: " + ex.RequestInformation.ExtendedErrorInformation.ErrorCode);
            }
            catch (FaceAPIException ex)
            {
                Console.WriteLine("Face API error: " + ex.ErrorCode + ex.ErrorMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }

        }

        /// <summary>
        /// Create a container for the sample application to process blobs in. 
        /// </summary>
        /// <returns>A <see cref="Task{T}"/> object of type <see cref="CloudBlobContainer"/> that represents an asynchronous operation</returns>
        private static async Task<CloudBlobContainer> CreateBlobContainerAsync()
        {
            // Retrieve storage account information from connection string.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));

            // Create a queue client for interacting with the queue service
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            Console.WriteLine("Creating a container for the demo");
            CloudBlobContainer container = blobClient.GetContainerReference("samplecontainer");
            try
            {
                await container.CreateIfNotExistsAsync();
            }
            catch (StorageException ex)
            {
                Console.WriteLine("Error: " + ex.RequestInformation.ExtendedErrorInformation.ErrorCode + ex.RequestInformation.ExtendedErrorInformation.ErrorMessage);
                Console.ReadLine();
                throw;
            }

            return container;
        }

        /// <summary>
        /// Create a queue for the sample application to process messages in. 
        /// </summary>
        /// <returns>A <see cref="Task{T}"/> object of type <see cref="CloudQueue"/> that represents an asynchronous operation</returns>
        private static async Task<CloudQueue> CreateQueueAsync()
        {
            // Retrieve storage account information from connection string.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));

            // Create a queue client for interacting with the queue service
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();

            Console.WriteLine("Creating a queue for the demo");
            CloudQueue queue = queueClient.GetQueueReference("samplequeue");
            try
            {
                await queue.CreateIfNotExistsAsync();
            }
            catch (StorageException ex)
            {
                Console.WriteLine("Error: " + ex.RequestInformation.ExtendedErrorInformation.ErrorCode + ex.RequestInformation.ExtendedErrorInformation.ErrorMessage);
                Console.ReadLine();
                throw;
            }

            return queue;
        }

        /// <summary>
        /// Create a table for the sample application to store metadata in. 
        /// </summary>
        /// <returns>A <see cref="Task{T}"/> object of type <see cref="CloudTable"/> that represents an asynchronous operation</returns>
        private static async Task<CloudTable> CreateTableAsync()
        {
            // Retrieve storage account information from connection string.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));

            // Create a table client for interacting with the queue service
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

            Console.WriteLine("Creating a table for the demo");
            CloudTable table = tableClient.GetTableReference("sampletable");
            try
            {
                await table.CreateIfNotExistsAsync();
            }
            catch (StorageException ex)
            {
                Console.WriteLine("Error: " + ex.RequestInformation.ExtendedErrorInformation.ErrorCode + ex.RequestInformation.ExtendedErrorInformation.ErrorMessage);
                Console.ReadLine();
                throw;
            }

            return table;
        }

        /// <summary>
        /// Uploads all the images that reside in 'testfolder' and then adds
        /// the age of the person as a metadata after calling the Face API.
        /// Once done, the message gets deleted from the queue
        /// </summary>
        /// <param name="queue">The sample queue</param>
        /// <param name="container">The sample blob container</param>
        /// <param name="table">The sample table</param>
        /// <returns></returns>
        private static async Task cleanup(CloudQueue queue, CloudBlobContainer container, CloudTable table)
        {

            try
            {

                Console.WriteLine("Cleaning up the queue, table and the blobs created");

                await queue.DeleteIfExistsAsync();

                await container.DeleteIfExistsAsync();

                await table.DeleteIfExistsAsync();
               
            }
            catch (StorageException ex)
            {
                Console.WriteLine("Storage error: " + ex.RequestInformation.ExtendedErrorInformation.ErrorCode);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                Console.ReadLine();
            }

        }

    }
}
