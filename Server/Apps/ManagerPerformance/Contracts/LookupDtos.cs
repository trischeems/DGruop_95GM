namespace DGroup.Server.Apps.ManagerPerformance.Contracts;

// Danh muc nen (lookups): don vi tinh, kho, nhom NVL.

/// <summary>Don vi tinh tra ve client (khop cot bang units_of_measure).</summary>
public sealed record UomDto(long Id, string Code, string Name);

/// <summary>Yeu cau tao don vi tinh moi.</summary>
public sealed record CreateUomRequest(string Code, string Name);

/// <summary>Kho tra ve client (khop cot bang warehouses).</summary>
public sealed record WarehouseDto(long Id, string Code, string Name, bool IsActive);

/// <summary>Yeu cau tao kho moi.</summary>
public sealed record CreateWarehouseRequest(string Code, string Name);

/// <summary>Nhom NVL tra ve client (khop cot bang material_categories).</summary>
public sealed record MaterialCategoryDto(long Id, string Code, string Name);

/// <summary>Yeu cau tao nhom NVL moi.</summary>
public sealed record CreateCategoryRequest(string Code, string Name);
