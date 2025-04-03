-- 创建存储过程模板
CREATE PROCEDURE [HumanResources].[uspEmployeeSalaryAnalysis]
    @DepartmentFilter NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- CTE1：基础员工薪资数据
    WITH SalaryData AS (
        SELECT 
            e.EmployeeID,
            e.FirstName,
            e.LastName,
            e.JobTitle,
            d.Name AS DepartmentName,
            s.Rate AS HourlyRate,
            e.HireDate,
            CASE 
                WHEN e.MaritalStatus = 'M' THEN '已婚'
                WHEN e.MaritalStatus = 'S' THEN '单身'
                ELSE '其他'
            END AS MaritalStatus
        FROM 
            HumanResources.Employee e
        LEFT JOIN 
            HumanResources.EmployeeDepartment ed ON e.EmployeeID = ed.EmployeeID
        LEFT JOIN 
            HumanResources.Department d ON ed.DepartmentID = d.DepartmentID
        LEFT JOIN 
            HumanResources.EmployeePayHistory s ON e.EmployeeID = s.EmployeeID
    ),
    
    -- CTE2：部门薪资统计
    DepartmentStats AS (
        SELECT 
            DepartmentName,
            AVG(HourlyRate) AS AvgRate,
            MIN(HourlyRate) AS MinRate,
            MAX(HourlyRate) AS MaxRate,
            COUNT(*) AS EmployeeCount
        FROM 
            SalaryData
        GROUP BY 
            DepartmentName
    ),
    
    -- CTE3：薪资等级分类
    SalaryGrade AS (
        SELECT 
            EmployeeID,
            CASE 
                WHEN HourlyRate < (SELECT MinRate FROM DepartmentStats WHERE DepartmentName = SalaryData.DepartmentName) + 10 THEN '低'
                WHEN HourlyRate BETWEEN (SELECT MinRate FROM DepartmentStats WHERE DepartmentName = SalaryData.DepartmentName) + 10 
                     AND (SELECT MaxRate FROM DepartmentStats WHERE DepartmentName = SalaryData.DepartmentName) - 10 THEN '中'
                ELSE '高'
            END AS SalaryGrade
        FROM 
            SalaryData
    )
    
    -- 主查询：合并结果并过滤部门
    SELECT 
        sd.FirstName,
        sd.LastName,
        sd.JobTitle,
        sd.DepartmentName,
        sd.HourlyRate,
        sd.MaritalStatus,
        CASE 
            WHEN sd.HireDate < DATEADD(year, -5, GETDATE()) THEN '资深'
            ELSE '新员工'
        END AS Tenure,
        ds.AvgRate AS DepartmentAvgRate,
        sg.SalaryGrade
    FROM 
        SalaryData sd
    LEFT JOIN 
        DepartmentStats ds ON sd.DepartmentName = ds.DepartmentName
    LEFT JOIN 
        SalaryGrade sg ON sd.EmployeeID = sg.EmployeeID
    WHERE 
        (@DepartmentFilter IS NULL OR sd.DepartmentName = @DepartmentFilter)
    ORDER BY 
        ds.AvgRate DESC, sd.LastName, sd.FirstName;
END