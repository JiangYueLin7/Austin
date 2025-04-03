-- �����洢����ģ��
CREATE PROCEDURE [Production].[uspProductInventoryTrend]
    @StartDate DATE = '20240101',
    @EndDate DATE = '20250101'
AS
BEGIN
    SET NOCOUNT ON;

    -- CTE1���������
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
    
    -- CTE2����������
    SalesData AS (
        SELECT 
            soh.SalesOrderID,
            p.ProductID,
            p.Name AS ProductName,
            SUM(sod.OrderQty) AS TotalSold,
            SUM(sod.LineTotal) AS TotalRevenue,
            CASE 
                WHEN soh.OrderDate < DATEADD(quarter, -1, GETDATE()) THEN '�ɶ���'
                ELSE '�¶���'
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
    
    -- CTE3���������������
    TrendAnalysis AS (
        SELECT 
            i.ProductID,
            i.ProductName,
            i.StandardCost,
            i.OnHandQuantity,
            COALESCE(s.TotalSold, 0) AS TotalSold,
            COALESCE(s.TotalRevenue, 0) AS TotalRevenue,
            CASE 
                WHEN i.OnHandQuantity > (SELECT AVG(OnHandQuantity) FROM InventoryData) THEN '��ʣ'
                WHEN i.OnHandQuantity < (SELECT AVG(OnHandQuantity) FROM InventoryData) THEN '��ȱ'
                ELSE '����'
            END AS InventoryStatus,
            CASE 
                WHEN s.TotalSold > (SELECT AVG(TotalSold) FROM SalesData) THEN '����'
                ELSE '����'
            END AS SalesPerformance
        FROM 
            InventoryData i
        LEFT JOIN 
            SalesData s ON i.ProductID = s.ProductID
    )
    
    -- ����ѯ���ϲ���������ʱ��ά��
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
            WHEN DATEDIFF(day, ta.ModifiedDate, GETDATE()) < 30 THEN '���ڸ���'
            ELSE '��ʷ����'
        END AS DataAge
    FROM 
        TrendAnalysis ta
    ORDER BY 
        ta.TotalRevenue DESC, ta.ProductName;
END