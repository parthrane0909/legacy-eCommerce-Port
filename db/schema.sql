-- LegacyEcommerceDb schema
IF DB_ID('LegacyEcommerceDb') IS NULL CREATE DATABASE LegacyEcommerceDb;
GO
USE LegacyEcommerceDb;
GO
IF OBJECT_ID('dbo.OrderLines') IS NOT NULL DROP TABLE dbo.OrderLines;
IF OBJECT_ID('dbo.Orders') IS NOT NULL DROP TABLE dbo.Orders;
IF OBJECT_ID('dbo.CartItems') IS NOT NULL DROP TABLE dbo.CartItems;
IF OBJECT_ID('dbo.ProductVariants') IS NOT NULL DROP TABLE dbo.ProductVariants;
IF OBJECT_ID('dbo.ProductImages') IS NOT NULL DROP TABLE dbo.ProductImages;
IF OBJECT_ID('dbo.Products') IS NOT NULL DROP TABLE dbo.Products;
IF OBJECT_ID('dbo.Categories') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Addresses') IS NOT NULL DROP TABLE dbo.Addresses;
GO
CREATE TABLE dbo.Categories (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Slug NVARCHAR(120) NOT NULL,
    Description NVARCHAR(500) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    SortOrder INT NOT NULL DEFAULT 0,
    ParentId INT NULL REFERENCES dbo.Categories(Id)
);
CREATE TABLE dbo.Products (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    Slug NVARCHAR(220) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Price DECIMAL(18,2) NOT NULL,
    CategoryId INT NOT NULL REFERENCES dbo.Categories(Id),
    IsFeatured BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    DiscountPercent INT NULL,
    RatingNum DECIMAL(3,2) NULL,
    Stock INT NULL
);
CREATE TABLE dbo.ProductImages (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ProductId INT NOT NULL REFERENCES dbo.Products(Id),
    Url NVARCHAR(500) NOT NULL,
    AltText NVARCHAR(200) NULL,
    IsPrimary BIT NOT NULL DEFAULT 0,
    SortOrder INT NOT NULL DEFAULT 0
);
CREATE TABLE dbo.ProductVariants (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ProductId INT NOT NULL REFERENCES dbo.Products(Id),
    Name NVARCHAR(100) NOT NULL,
    Sku NVARCHAR(50) NULL,
    PriceDelta DECIMAL(18,2) NOT NULL DEFAULT 0,
    Stock INT NOT NULL DEFAULT 0
);
CREATE TABLE dbo.CartItems (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SessionId NVARCHAR(100) NOT NULL,
    ProductId INT NOT NULL,
    VariantId INT NULL,
    ProductName NVARCHAR(200) NOT NULL,
    VariantName NVARCHAR(100) NULL,
    ImageUrl NVARCHAR(500) NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    Quantity INT NOT NULL
);
CREATE TABLE dbo.Orders (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    OrderNumber NVARCHAR(30) NOT NULL,
    CustomerId NVARCHAR(128) NOT NULL,
    Status NVARCHAR(30) NOT NULL DEFAULT 'Placed',
    Subtotal DECIMAL(18,2) NOT NULL,
    ShippingCost DECIMAL(18,2) NOT NULL,
    Total DECIMAL(18,2) NOT NULL,
    ShipFullName NVARCHAR(150) NULL,
    ShipLine1 NVARCHAR(300) NULL,
    ShipCity NVARCHAR(100) NULL,
    ShipState NVARCHAR(100) NULL,
    ShipPostalCode NVARCHAR(20) NULL,
    ShipCountry NVARCHAR(100) NULL,
    ShipPhone NVARCHAR(50) NULL,
    ShippingMethod NVARCHAR(50) NULL,
    PaymentMethod NVARCHAR(50) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
);
CREATE TABLE dbo.OrderLines (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    OrderId INT NOT NULL REFERENCES dbo.Orders(Id),
    ProductId INT NOT NULL,
    ProductName NVARCHAR(200) NOT NULL,
    VariantName NVARCHAR(100) NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    Quantity INT NOT NULL,
    LineTotal DECIMAL(18,2) NOT NULL
);
CREATE TABLE dbo.Addresses (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId NVARCHAR(128) NOT NULL,
    FullName NVARCHAR(150) NULL,
    Line1 NVARCHAR(300) NULL,
    Line2 NVARCHAR(300) NULL,
    City NVARCHAR(100) NULL,
    State NVARCHAR(100) NULL,
    PostalCode NVARCHAR(20) NULL,
    Country NVARCHAR(100) NULL,
    Phone NVARCHAR(50) NULL,
    IsDefault BIT NOT NULL DEFAULT 0
);
GO
USE LegacyEcommerceDb;
GO
CREATE TABLE dbo.AspNetUsers (
    Id NVARCHAR(128) NOT NULL PRIMARY KEY,
    Email NVARCHAR(256) NULL,
    EmailConfirmed BIT NOT NULL DEFAULT 0,
    PasswordHash NVARCHAR(MAX) NULL,
    SecurityStamp NVARCHAR(MAX) NULL,
    PhoneNumber NVARCHAR(MAX) NULL,
    PhoneNumberConfirmed BIT NOT NULL DEFAULT 0,
    TwoFactorEnabled BIT NOT NULL DEFAULT 0,
    LockoutEndDateUtc DATETIME NULL,
    LockoutEnabled BIT NOT NULL DEFAULT 0,
    AccessFailedCount INT NOT NULL DEFAULT 0,
    UserName NVARCHAR(256) NOT NULL,
    Discriminator NVARCHAR(128) NULL,
    FullName NVARCHAR(150) NULL
);
CREATE TABLE dbo.AspNetRoles (
    Id NVARCHAR(128) NOT NULL PRIMARY KEY,
    Name NVARCHAR(256) NULL
);
CREATE TABLE dbo.AspNetUserClaims (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(128) NOT NULL,
    ClaimType NVARCHAR(MAX) NULL,
    ClaimValue NVARCHAR(MAX) NULL
);
CREATE TABLE dbo.AspNetUserLogins (
    LoginProvider NVARCHAR(128) NOT NULL,
    ProviderKey NVARCHAR(128) NOT NULL,
    UserId NVARCHAR(128) NOT NULL,
    PRIMARY KEY (LoginProvider, ProviderKey, UserId)
);
CREATE TABLE dbo.AspNetUserRoles (
    UserId NVARCHAR(128) NOT NULL,
    RoleId NVARCHAR(128) NOT NULL,
    PRIMARY KEY (UserId, RoleId)
);
GO
