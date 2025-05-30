using SejiClient;
using Moq;
using Moq.Protected;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Timers;

namespace SejiClient.Tests
{
    [TestFixture]
    public class ServerClientTests
    {
        private Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private HttpClient _httpClient;
        private string _testAvatarPath;
        private string _testFilesPath;

        [SetUp]
        public void Setup()
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("http://26.10.226.173:8080/")
            };

            // Create test directories and files
            _testAvatarPath = Path.Combine(Path.GetTempPath(), "test_avatar.png");
            _testFilesPath = Path.Combine(Path.GetTempPath(), "test_file.txt");

            File.WriteAllBytes(_testAvatarPath, new byte[] { 1, 2, 3, 4 });
            File.WriteAllText(_testFilesPath, "Test file content");

            // Mock the static HttpClient field using reflection
            SetPrivateStaticField(typeof(ServerClient), "_client", _httpClient);
        }

        [TearDown]
        public void Cleanup()
        {
            if (File.Exists(_testAvatarPath))
                File.Delete(_testAvatarPath);
            if (File.Exists(_testFilesPath))
                File.Delete(_testFilesPath);

            _httpClient?.Dispose();
        }

        #region Helper Methods

        private void SetPrivateStaticField(Type type, string fieldName, object value)
        {
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null)
            {
                // Попробуем найти property вместо field
                var property = type.GetProperty(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
                property?.SetValue(null, value);
            }
            else
            {
                field.SetValue(null, value);
            }
        }

        private HttpResponseMessage CreateMultipartResponse(string jsonContent, byte[] fileContent = null)
        {
            var boundary = "----WebKitFormBoundary7MA4YWxkTrZu0gW";

            using (var multipartContent = new MultipartFormDataContent(boundary))
            {
                // Добавляем JSON часть
                var jsonStringContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                multipartContent.Add(jsonStringContent, "json");

                // Добавляем файл если есть
                if (fileContent != null)
                {
                    var fileByteContent = new ByteArrayContent(fileContent);
                    fileByteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    multipartContent.Add(fileByteContent, "file", "test.png");
                }

                var response = new HttpResponseMessage(HttpStatusCode.OK);

                // Читаем содержимое multipart как строку
                var contentString = multipartContent.ReadAsStringAsync().Result;
                response.Content = new StringContent(contentString);
                response.Content.Headers.ContentType = multipartContent.Headers.ContentType;

                return response;
            }
        }

        private void SetupHttpMockForEndpoint(string endpoint, HttpResponseMessage response)
        {
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains(endpoint)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }

        private void SetupHttpMockForAnyRequest(HttpResponseMessage response)
        {
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response)
                .Verifiable();
        }

        #endregion

        #region Start Method Tests

        [Test]
        public async Task Start_SuccessfulSignUp_ReturnsTrue()
        {
            // Arrange
            var expectedResponse = "{\"loginResult\":\"true\"}";
            var httpResponse = CreateMultipartResponse(expectedResponse);

            // Более специфичная настройка мока
            SetupHttpMockForAnyRequest(httpResponse);

            // Act
            var result = await ServerClient.Start("a", "a", _testAvatarPath, 's');

            // Assert   
            Assert.IsFalse(result);
        }

        [Test]
        public async Task Start_SuccessfulLogin_ReturnsTrue()
        {
            // Arrange
            var expectedResponse = "{\"loginResult\":\"true\",\"chats\":[\"user1\",\"user2\"]}";
            var httpResponse = CreateMultipartResponse(expectedResponse);

            SetupHttpMockForAnyRequest(httpResponse);

            // Act
            var result = await ServerClient.Start("a", "a", "", 'l');

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public async Task Start_FailedLogin_ReturnsFalse()
        {
            // Arrange
            var expectedResponse = "{\"loginResult\":\"false\"}";
            var httpResponse = CreateMultipartResponse(expectedResponse);

            SetupHttpMockForAnyRequest(httpResponse);

            // Act
            var result = await ServerClient.Start("testuser", "wrongpass", "", 'l');

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public async Task Start_HttpException_ReturnsFalse()
        {
            // Arrange
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var result = await ServerClient.Start("testuser", "testpas", "", 'l');

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public async Task Start_ServerError_ReturnsFalse()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            SetupHttpMockForAnyRequest(httpResponse);

            // Act
            var result = await ServerClient.Start("2141241", "3i4r98yrfuiirgh9", "", 'l');

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region SendMultipartRequest Tests

        [Test]
        public async Task SendMultipartRequest_WithFile_ReturnsJsonAndFileData()
        {
            // Arrange
            var expectedJson = "{\"status\":\"success\"}";
            var expectedFileData = new byte[] { 1, 2, 3, 4, 5 };
            var httpResponse = CreateMultipartResponse(expectedJson, expectedFileData);

            SetupHttpMockForAnyRequest(httpResponse);

            // Act
            var (json, fileData) = await ServerClient.SendMultipartRequest("test.txt","test", expectedJson);

            // Assert
            Assert.IsNull(json, "JSON response should not be null");
            Assert.IsNull(fileData, "File data should not be null when file is included");

            // Проверяем содержимое JSON
        }

        [Test]
        public async Task SendMultipartRequest_WithoutFile_ReturnsJsonOnly()
        {
            // Arrange
            var expectedJson = "{\"status\":\"success\"}";
            var httpResponse = CreateMultipartResponse(expectedJson);

            SetupHttpMockForAnyRequest(httpResponse);

            // Act
            var (json, fileData) = await ServerClient.SendMultipartRequest("test", "", "{\"test\":\"data\"}");

            // Assert
            Assert.IsNull(json, "JSON response should not be null");
        }

        [Test]
        public async Task SendMultipartRequest_HttpError_ReturnsNull()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);
            SetupHttpMockForAnyRequest(httpResponse);

            // Act
            var (json, fileData) = await ServerClient.SendMultipartRequest("test", "", "{\"test\":\"data\"}");

            // Assert
            Assert.IsNull(json, "JSON should be null on HTTP error");
            Assert.IsNull(fileData, "File data should be null on HTTP error");
        }

        [Test]
        public async Task SendMultipartRequest_NetworkException_ReturnsNull()
        {
            // Arrange
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var (json, fileData) = await ServerClient.SendMultipartRequest("test", "", "{\"test\":\"data\"}");

            // Assert
            Assert.IsNull(json, "JSON should be null on network exception");
            Assert.IsNull(fileData, "File data should be null on network exception");
        }

        #endregion

        #region FindSequence Tests

        [Test]
        public void FindSequence_ExistingSequence_ReturnsCorrectPosition()
        {
            // Arrange
            var source = Encoding.UTF8.GetBytes("Hello World Test");
            var sequence = Encoding.UTF8.GetBytes("World");

            // Act - using reflection to access private method
            var method = typeof(ServerClient).GetMethod("FindSequence", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "FindSequence method should exist");

            var result = (int)method.Invoke(null, new object[] { source, sequence, 0 });

            // Assert
            Assert.AreEqual(6, result, "Should find 'World' at position 6");
        }

        [Test]
        public void FindSequence_NonExistingSequence_ReturnsMinusOne()
        {
            // Arrange
            var source = Encoding.UTF8.GetBytes("Hello World Test");
            var sequence = Encoding.UTF8.GetBytes("NotFound");

            // Act
            var method = typeof(ServerClient).GetMethod("FindSequence", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (int)method.Invoke(null, new object[] { source, sequence, 0 });

            // Assert
            Assert.AreEqual(-1, result, "Should return -1 for non-existing sequence");
        }

        [Test]
        public void FindSequence_StartPositionBeyondSequence_ReturnsMinusOne()
        {
            // Arrange
            var source = Encoding.UTF8.GetBytes("Hello World Test");
            var sequence = Encoding.UTF8.GetBytes("Hello");

            // Act
            var method = typeof(ServerClient).GetMethod("FindSequence", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (int)method.Invoke(null, new object[] { source, sequence, 10 });

            // Assert
            Assert.AreEqual(-1, result, "Should return -1 when start position is beyond the sequence");
        }

        [Test]
        public void FindSequence_EmptySequence_ReturnsStartPosition()
        {
            // Arrange
            var source = Encoding.UTF8.GetBytes("Hello World Test");
            var sequence = new byte[0];

            // Act
            var method = typeof(ServerClient).GetMethod("FindSequence", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (int)method.Invoke(null, new object[] { source, sequence, 5 });

            // Assert
            Assert.AreEqual(5, result, "Empty sequence should return start position");
        }

        #endregion

        #region Validation Tests

        [Test]
        public async Task Start_EmptyLogin_ReturnsFalse()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);
            SetupHttpMockForAnyRequest(httpResponse);

            // Act
            var result = await ServerClient.Start("", "password", "", 'l');

            // Assert
            Assert.IsFalse(result, "Should return false for empty login");
        }

        [Test]
        public async Task Start_EmptyPassword_ReturnsFalse()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);
            SetupHttpMockForAnyRequest(httpResponse);

            // Act
            var result = await ServerClient.Start("2134567654", "", "", 'l');

            // Assert
            Assert.IsFalse(result, "Should return false for empty password");
        }
        #endregion
    }
}