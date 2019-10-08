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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ProjectOxford.Face;

    /// <summary>
    /// Azure Storage Queue Service Pop Receipt Sample - this sample demonstrates how to use the new Popreceipt functionality to coordinate 
    /// updates across two non-transactional resources. In this case it allows a Blob to be published and meta-data about the Blob 
    /// to be written to a table entity. Prior to uploading the blob, a message is enqueued, the updates are performed to blob and 
    /// table and if successful the message is deleted using its popreceipt. Any messages remaining in the queue need additional work 
    /// due to the failures experienced, which can be consumed via a backend worker that is not demonstrated in this sample.    
    /// 
    /// Note: This sample uses asynchronous programming model with Task Parallel Library (TPL) to demonstrate how to call the Storage Service using the 
    /// storage client libraries asynchronous API's. When used in real applications this approach enables you to improve the 
    /// responsiveness of your application. Calls to the storage service are prefixed by the await keyword. 
    /// 
    /// Documentation References: 
    /// - What is a Storage Account - http://azure.microsoft.com/en-us/documentation/articles/storage-whatis-account/
    /// - Getting Started with Queues - http://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-queues/
    /// - Queue Service Concepts - http://msdn.microsoft.com/en-us/library/dd179353.aspx
    /// - Queue Service C# API - http://go.microsoft.com/fwlink/?LinkID=398944
    /// - Get started with Azure Blob storage using .NET - https://docs.microsoft.com/en-us/azure/storage/storage-dotnet-how-to-use-blobs
    /// - Get started with Azure Table storage using .NET - https://docs.microsoft.com/en-us/azure/storage/storage-dotnet-how-to-use-tables
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
        //      2. Create a folder named 'testfolder' locally (~ bin/Debug) and place a few photos
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

            Console.WriteLine("Azure Storage Queue Sample demonstrating popreceipt functionality\n");

            MainAsync(args).Wait();

            Console.WriteLine("Press any key to exit");
            Console.Read();

        }

        private static async Task MainAsync(string[] args)
        {
            
            // Create or reference an existing queue, container and a table 
            CloudQueue queue = await CreateQueueAsync();
            CloudBlobContainer container = await CreateBlobContainerAsync();
            CloudTable table = await CreateTableAsync();

            // Face API Client
            // Add your Face subscription key to your environment variables
            FaceServiceClient faceClient = new FaceServiceClient(CloudConfigurationManager.GetSetting(Environment.GetEnvironmentVariable("FACE_SUBSCRIPTION_KEY")));

            try
            {
                // Process images to a blob account and save the estimated age as an entity in the table. Delete the messages in the queue using popreceipt
                await ProcessImages(queue, container, table, faceClient);
            }
            finally
            {
                // Clean up resources created by this sample. 
                await Cleanup(queue, container, table);
            }
            
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
        /// <returns>A task representing the asynchronous operation</returns>
        private static async Task ProcessImages(CloudQueue queue, CloudBlobContainer container, CloudTable table, FaceServiceClient faceClient)
        {

            try
            {
                // Iterate over photos in 'testfolder'
                var images = Directory.EnumerateFiles("testfolder", "*.jpg");

                foreach (string currentFile in images)
                {

                    string fileName = currentFile.Replace("testfolder\\", "");

                    Console.WriteLine("Processing image {0}", fileName);

                    // Add a message to the queue for each photo. Note the visibility timeout
                    // as blob and table operations in the following process may take up to 900 seconds.
                    // For simplicity, other factors like FaceAPI call timeout are ignored.
                    // After the 900 seconds, the message will be visible and a worker role can pick up 
                    // the message from queue for cleanup. Default time to live for the message is 7 days.
                    CloudQueueMessage message = new CloudQueueMessage(fileName);
                    queue.AddMessage(message, null, TimeSpan.FromSeconds(900));

                    // read the file
                    using (var fileStream = File.OpenRead(currentFile))
                    {

                        // detect face and estimate the age
                        var faces = await faceClient.DetectAsync(fileStream, false, true, 
                            new FaceAttributeType[] {FaceAttributeType.Age});
                        Console.WriteLine(faces.Length + " face(s) detected in " + fileName);

                        CloudBlockBlob blob = container.GetBlockBlobReference(fileName);

                        var tableEntity = new DynamicTableEntity("FaceImages", fileName);

                        // iterate over detected faces
                        int i = 1;
                        foreach (var face in faces)
                        {

                            // append the age info as property in the table entity
                            tableEntity.Properties.Add("person" + i.ToString(),
                                new EntityProperty(face.FaceAttributes.Age.ToString()));
                            i++;

                            // ignore if more than 250 faces were found. An entity can contain up to 252 properties.
                            if (i > 250)
                                break;

                        }

                        // upload the blob if a face was detected
                        if (faces.Length > 0)
                            await blob.UploadFromFileAsync(currentFile);

                        // store the age info in the table
                        table.Execute(TableOperation.InsertOrReplace(tableEntity));

                        // delete the queue message with the pop receipt if previous operations completed successfully
                        if (blob.Exists() && table.Execute(TableOperation.Retrieve("FaceImages", fileName)).HttpStatusCode == 200)
                            await queue.DeleteMessageAsync(message.Id, message.PopReceipt);

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

                if(ex is DirectoryNotFoundException || ex is FileNotFoundException)
                    Console.WriteLine("Please make sure that the folder \"testfolder\" (with images) is present in the current directory where the sample is running");

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

            // Create a blob client for interacting with the blob service
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Get a reference to the container
            Console.WriteLine("Creating a container for the demo");
            CloudBlobContainer container = blobClient.GetContainerReference("samplecontainer");

            try
            {
                await container.CreateIfNotExistsAsync();
            }
            catch (StorageException ex)
            {
                Console.WriteLine("Error: " + ex.RequestInformation.ExtendedErrorInformation.ErrorCode + ex.RequestInformation.ExtendedErrorInformation.ErrorMessage);
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

            // Create a table client for interacting with the table service
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
                throw;
            }

            return table;
        }

        /// <summary>
        /// Cleans up all the created service resources by this sample
        /// </summary>
        /// <param name="queue">The sample queue</param>
        /// <param name="container">The sample blob container</param>
        /// <param name="table">The sample table</param>
        /// <returns>A task representing the asynchronous operation</returns>
        private static async Task Cleanup(CloudQueue queue, CloudBlobContainer container, CloudTable table)
        {

            Console.WriteLine("Cleaning up the queue, table and the blobs created");

            // Clean up the queue
            try
            {
                await queue.DeleteIfExistsAsync();
            }
            catch (StorageException ex)
            {
                Console.WriteLine("Storage error: " + ex.RequestInformation.ExtendedErrorInformation.ErrorCode);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }

            // Clean up the container
            try
            {
                await container.DeleteIfExistsAsync();
            }
            catch (StorageException ex)
            {
                Console.WriteLine("Storage error: " + ex.RequestInformation.ExtendedErrorInformation.ErrorCode);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }

            // Clean up the table
            try
            {
                await table.DeleteIfExistsAsync();
            }
            catch (StorageException ex)
            {
                Console.WriteLine("Storage error: " + ex.RequestInformation.ExtendedErrorInformation.ErrorCode);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }

        }

    }
}
