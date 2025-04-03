-- 创建存储过程模板
CREATE PROCEDURE [Production].[uspProductInventoryTrend]
    @StartDate DATE = '20240101',
    @EndDate DATE = '20250101'
AS
BEGIN
    SET NOCOUNT ON;

    -- CTE1：库存数据
    WITH InventoryData AS (
        SELECT 
            p.ProductID,
            p.Name AS ProductName,
            p.StandardCost,
            inv.Quantity AS OnHandQuantity,
            inv.ModifiedDate
        FROM 
            Production.Product p
        LEFT JOIN 
            Production.ProductInventory inv ON p.ProductID = inv.ProductID
    ),
    
    -- CTE2：销售数据
    SalesData AS (
        SELECT 
            soh.SalesOrderID,
            p.ProductID,
            p.Name AS ProductName,
            SUM(sod.OrderQty) AS TotalSold,
            SUM(sod.LineTotal) AS TotalRevenue,
            CASE 
                WHEN soh.OrderDate < DATEADD(quarter, -1, GETDATE()) THEN '旧订单'
                ELSE '新订单'
            END AS OrderAge
        FROM 
            Sales.SalesOrderHeader soh
        INNER JOIN 
            Sales.SalesOrderDetail sod ON soh.SalesOrderID = sod.SalesOrderID
        INNER JOIN 
            Production.Product p ON sod.ProductID = p.ProductID
        WHERE 
            soh.OrderDate BETWEEN @StartDate AND @EndDate
        GROUP BY 
            soh.SalesOrderID, p.ProductID, p.Name, soh.OrderDate
    ),
    
    -- CTE3：库存与销售趋势
    TrendAnalysis AS (
        SELECT 
            i.ProductID,
            i.ProductName,
            i.StandardCost,
            i.OnHandQuantity,
            COALESCE(s.TotalSold, 0) AS TotalSold,
            COALESCE(s.TotalRevenue, 0) AS TotalRevenue,
            CASE 
                WHEN i.OnHandQuantity > (SELECT AVG(OnHandQuantity) FROM InventoryData) THEN '过剩'
                WHEN i.OnHandQuantity < (SELECT AVG(OnHandQuantity) FROM InventoryData) THEN '紧缺'
                ELSE '正常'
            END AS InventoryStatus,
            CASE 
                WHEN s.TotalSold > (SELECT AVG(TotalSold) FROM SalesData) THEN '畅销'
                ELSE '滞销'
            END AS SalesPerformance
        FROM 
            InventoryData i
        LEFT JOIN 
            SalesData s ON i.ProductID = s.ProductID
    )
    
    -- 主查询：合并结果并添加时间维度
    SELECT 
        ta.ProductID,
        ta.ProductName,
        ta.StandardCost,
        ta.OnHandQuantity,
        ta.TotalSold,
        ta.TotalRevenue,
        ta.InventoryStatus,
        ta.SalesPerformance,
        CASE 
            WHEN DATEDIFF(day, ta.ModifiedDate, GETDATE()) < 30 THEN '近期更新'
            ELSE '历史数据'
        END AS DataAge
    FROM 
        TrendAnalysis ta
    ORDER BY 
        ta.TotalRevenue DESC, ta.ProductName;
END