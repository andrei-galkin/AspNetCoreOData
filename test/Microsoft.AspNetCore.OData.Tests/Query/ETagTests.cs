﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.AspNetCore.OData.Abstracts;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Tests.Commons;
using Microsoft.AspNetCore.OData.Tests.Extensions;
using Microsoft.Net.Http.Headers;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using Microsoft.OData.UriParser;
using Xunit;

namespace Microsoft.AspNetCore.OData.Tests.Query
{
    public class ETagTests
    {
        private readonly IList<ETagCustomer> _customers;

        public ETagTests()
        {
            _customers = new List<ETagCustomer>
                {
                    new ETagCustomer
                        {
                            ID = 1,
                            FirstName = "Foo",
                            LastName = "Bar",
                        },
                    new ETagCustomer
                        {
                            ID = 2,
                            FirstName = "Abc",
                            LastName = "Xyz",
                        },
                    new ETagCustomer
                        {
                            ID = 3,
                            FirstName = "Def",
                            LastName = "Xyz",
                        },
                };
        }

        [Fact]
        public void GetValue_Returns_SetValue()
        {
            // Arrange
            ETag etag = new ETag();

            // Act & Assert
            etag["Name"] = "Name1";
            Assert.Equal("Name1", etag["Name"]);
        }

        [Fact]
        public void DynamicGetValue_Returns_DynamicSetValue()
        {
            // Arrange
            dynamic etag = new ETag();

            // Act & Assert
            etag.Name = "Name1";
            Assert.Equal("Name1", etag.Name);
        }

        [Fact]
        public void GetValue_ThrowsInvalidOperation_IfNotWellFormed()
        {
            // Arrange
            ETag etag = new ETag();
            etag["Name"] = "Name1";
            etag.IsWellFormed = false;

            // Act && Assert
            ExceptionAssert.Throws<InvalidOperationException>(() => etag["Name"], "The ETag is not well-formed.");
        }

        [Fact]
        public void DynamicGetValue_ThrowsInvalidOperation_IfNotWellFormed()
        {
            // Arrange
            ETag etag = new ETag();
            etag["Name"] = "Name1";
            etag.IsWellFormed = false;
            dynamic dynamicETag = etag;

            // Act && Assert
            ExceptionAssert.Throws<InvalidOperationException>(() => dynamicETag.Name, "The ETag is not well-formed.");
        }

        [Fact]
        public void ApplyTo_NewQueryReturned_GivenQueryable()
        {
            // Arrange
            ETag etagCustomer = new ETag { EntityType = typeof(ETagCustomer) };
            dynamic etag = etagCustomer;
            etag.FirstName = "Foo";

            // Act
            IQueryable queryable = etagCustomer.ApplyTo(_customers.AsQueryable());

            // Assert
            Assert.NotNull(queryable);
            IEnumerable<ETagCustomer> actualCustomers = Assert.IsAssignableFrom<IEnumerable<ETagCustomer>>(queryable);
            Assert.Equal(
                new[] { 1 },
                actualCustomers.Select(customer => customer.ID));
            MethodCallExpression methodCall = queryable.Expression as MethodCallExpression;
            Assert.NotNull(methodCall);
            Assert.Equal(2, methodCall.Arguments.Count);
            Assert.Equal(@"Param_0 => (Param_0.FirstName == value(Microsoft.AspNetCore.OData.Query.Container.LinqParameterContainer+TypedLinqParameterContainer`1[System.String]).TypedProperty)",
                methodCall.Arguments[1].ToString());
        }

        [Fact]
        public void ApplyTo_NewQueryReturned_GivenQueryableAndIsIfNoneMatch()
        {
            // Arrange
            ETag etagCustomer = new ETag { EntityType = typeof(ETagCustomer), IsIfNoneMatch = true };
            dynamic etag = etagCustomer;
            etag.LastName = "Xyz";

            // Act
            IQueryable queryable = etagCustomer.ApplyTo(_customers.AsQueryable());

            // Assert
            Assert.NotNull(queryable);
            IEnumerable<ETagCustomer> actualCustomers = Assert.IsAssignableFrom<IEnumerable<ETagCustomer>>(queryable);
            Assert.Equal(
                new[] { 1 },
                actualCustomers.Select(customer => customer.ID));
            MethodCallExpression methodCall = queryable.Expression as MethodCallExpression;
            Assert.NotNull(methodCall);
            Assert.Equal(2, methodCall.Arguments.Count);
            Assert.Equal(
                @"Param_0 => Not((Param_0.LastName == value(Microsoft.AspNetCore.OData.Query.Container.LinqParameterContainer+TypedLinqParameterContainer`1[System.String]).TypedProperty))",
                methodCall.Arguments[1].ToString());
        }

        [Fact]
        public void ApplyTo_NewQueryReturned_IsIfNoneMatchWithMultipleConcurrencyProperties()
        {
            // Arrange
            ETag etagCustomer = new ETag { EntityType = typeof(ETagCustomer), IsIfNoneMatch = true };
            dynamic etag = etagCustomer;
            etag.FirstName = "Def";
            etag.LastName = "Xyz";

            // Act
            IQueryable queryable = etagCustomer.ApplyTo(_customers.AsQueryable());

            // Assert
            Assert.NotNull(queryable);
            IEnumerable<ETagCustomer> actualCustomers = Assert.IsAssignableFrom<IEnumerable<ETagCustomer>>(queryable);
            Assert.Equal(
                new[] { 1, 2 },
                actualCustomers.Select(customer => customer.ID));
            MethodCallExpression methodCall = queryable.Expression as MethodCallExpression;
            Assert.NotNull(methodCall);
            Assert.Equal(2, methodCall.Arguments.Count);
            Assert.Equal(
                @"Param_0 => Not(((Param_0.FirstName == value(Microsoft.AspNetCore.OData.Query.Container.LinqParameterContainer+TypedLinqParameterContainer`1[System.String]).TypedProperty) "
                + "AndAlso (Param_0.LastName == value(Microsoft.AspNetCore.OData.Query.Container.LinqParameterContainer+TypedLinqParameterContainer`1[System.String]).TypedProperty)))",
                methodCall.Arguments[1].ToString());
        }

        [Fact]
        public void ApplyTo_SameQueryReturned_GivenQueryableAndETagAny()
        {
            // Arrange
            var any = new ETag { IsAny = true };
            var customers = _customers.AsQueryable();

            // Act
            var queryable = any.ApplyTo(customers);

            // Assert
            Assert.NotNull(queryable);
            Assert.Same(queryable, customers);
        }

        [Theory]
        [InlineData(1.0, true, new[] { 1, 3 })]
        [InlineData(1.0, false, new[] { 2 })]
        [InlineData(1.1, true, null)]
        [InlineData(1.1, false, new[] { 1, 2, 3 })]
        public void ApplyTo_NewQueryReturned_ForDouble(double value, bool ifMatch, IList<int> expect)
        {
            // Arrange
            var myCustomers = new List<MyETagCustomer>
            {
                new MyETagCustomer
                {
                    ID = 1,
                    DoubleETag = 1.0,
                },
                new MyETagCustomer
                {
                    ID = 2,
                    DoubleETag = 1.1,
                },
                new MyETagCustomer
                {
                    ID = 3,
                    DoubleETag = 1.0,
                },
            };

            IETagHandler handerl = new DefaultODataETagHandler();
            Dictionary<string, object> properties = new Dictionary<string, object> { { "DoubleETag", value } };
            EntityTagHeaderValue etagHeaderValue = handerl.CreateETag(properties);

            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<MyETagCustomer>("Customers");
            IEdmModel model = builder.GetEdmModel();
            IEdmEntityType customer = model.SchemaElements.OfType<IEdmEntityType>().FirstOrDefault(e => e.Name == "MyEtagCustomer");
            IEdmEntitySet customers = model.FindDeclaredEntitySet("Customers");
            ODataPath odataPath = new ODataPath(new[] { new EntitySetSegment(customers) });
            var request = RequestFactory.Create(model, opt => opt.AddModel(model));
            request.ODataFeature().Path = odataPath;

            ETag etagCustomer = request.GetETag(etagHeaderValue);
            etagCustomer.EntityType = typeof(MyETagCustomer);
            etagCustomer.IsIfNoneMatch = !ifMatch;

            // Act
            IQueryable queryable = etagCustomer.ApplyTo(myCustomers.AsQueryable());

            // Assert
            Assert.NotNull(queryable);
            IList<MyETagCustomer> actualCustomers = Assert.IsAssignableFrom<IEnumerable<MyETagCustomer>>(queryable).ToList();
            if (expect != null)
            {
                Assert.Equal(expect, actualCustomers.Select(c => c.ID));
            }

            MethodCallExpression methodCall = queryable.Expression as MethodCallExpression;
            Assert.NotNull(methodCall);
            Assert.Equal(2, methodCall.Arguments.Count);
            if (ifMatch)
            {
                Assert.Equal(
                    "Param_0 => (Param_0.DoubleETag == value(Microsoft.AspNetCore.OData.Query.Container.LinqParameterContainer+TypedLinqParameterContainer`1[System.Double]).TypedProperty)",
                    methodCall.Arguments[1].ToString());
            }
            else
            {
                Assert.Equal(
                    "Param_0 => Not((Param_0.DoubleETag == value(Microsoft.AspNetCore.OData.Query.Container.LinqParameterContainer+TypedLinqParameterContainer`1[System.Double]).TypedProperty))",
                    methodCall.Arguments[1].ToString());
            }
        }

        public class MyETagCustomer
        {
            public int ID { get; set; }

            [ConcurrencyCheck]
            public double DoubleETag { get; set; }
        }

        [Theory]
        [InlineData((sbyte)1, (short)1, true, new int[] {})]
        [InlineData((sbyte)1, (short)1, false, new[] { 1, 2, 3 })]
        [InlineData(SByte.MaxValue, Int16.MaxValue, true, new[] { 2 })]
        [InlineData(SByte.MaxValue, Int16.MaxValue, false, new[] { 1, 3 })]
        [InlineData(SByte.MinValue, Int16.MinValue, true, new[] { 3 })]
        [InlineData(SByte.MinValue, Int16.MinValue, false, new[] { 1, 2 })]
        public void ApplyTo_NewQueryReturned_ForInteger(sbyte byteVal, short shortVal, bool ifMatch, IList<int> expect)
        {
            // Arrange
            var mycustomers = new List<MyETagOrder>
            {
                new MyETagOrder
                {
                    ID = 1,
                    ByteVal = 7,
                    ShortVal = 8
                },
                new MyETagOrder
                {
                    ID = 2,
                    ByteVal = SByte.MaxValue,
                    ShortVal = Int16.MaxValue
                },
                new MyETagOrder
                {
                    ID = 3,
                    ByteVal = SByte.MinValue,
                    ShortVal = Int16.MinValue
                },
            };
            IETagHandler handerl = new DefaultODataETagHandler();
            Dictionary<string, object> properties = new Dictionary<string, object>
            {
                { "ByteVal", byteVal },
                { "ShortVal", shortVal }
            };
            EntityTagHeaderValue etagHeaderValue = handerl.CreateETag(properties, null);

            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<MyETagOrder>("Orders");
            IEdmModel model = builder.GetEdmModel();
            IEdmEntitySet orders = model.FindDeclaredEntitySet("Orders");
            ODataPath odataPath = new ODataPath(new[] {new EntitySetSegment(orders) });
            var request = RequestFactory.Create(model, opt => opt.AddModel(model));
            request.ODataFeature().Path = odataPath;

            ETag etagCustomer = request.GetETag(etagHeaderValue);
            etagCustomer.EntityType = typeof(MyETagOrder);
            etagCustomer.IsIfNoneMatch = !ifMatch;

            // Act
            IQueryable queryable = etagCustomer.ApplyTo(mycustomers.AsQueryable());

            // Assert
            Assert.NotNull(queryable);
            IEnumerable<MyETagOrder> actualOrders = Assert.IsAssignableFrom<IEnumerable<MyETagOrder>>(queryable);
            Assert.Equal(expect, actualOrders.Select(c => c.ID));
            MethodCallExpression methodCall = queryable.Expression as MethodCallExpression;
            Assert.NotNull(methodCall);
            Assert.Equal(2, methodCall.Arguments.Count);

            if (ifMatch)
            {
                Assert.Equal(
                    "Param_0 => ((Param_0.ByteVal == value(Microsoft.AspNetCore.OData.Query.Container.LinqParameterContainer+TypedLinqParameterContainer`1[System.SByte]).TypedProperty) " +
                    "AndAlso (Param_0.ShortVal == value(Microsoft.AspNetCore.OData.Query.Container.LinqParameterContainer+TypedLinqParameterContainer`1[System.Int16]).TypedProperty))",
                    methodCall.Arguments[1].ToString());
            }
            else
            {
                Assert.Equal(
                    "Param_0 => Not(((Param_0.ByteVal == value(Microsoft.AspNetCore.OData.Query.Container.LinqParameterContainer+TypedLinqParameterContainer`1[System.SByte]).TypedProperty) " +
                    "AndAlso (Param_0.ShortVal == value(Microsoft.AspNetCore.OData.Query.Container.LinqParameterContainer+TypedLinqParameterContainer`1[System.Int16]).TypedProperty)))",
                    methodCall.Arguments[1].ToString());
            }
        }

        public class MyETagOrder
        {
            public int ID { get; set; }

            [ConcurrencyCheck]
            public sbyte ByteVal { get; set; }

            [ConcurrencyCheck]
            public short ShortVal { get; set; }
        }

        public class ETagCustomer
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string City { get; set; }
        }
    }
}
