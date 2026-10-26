namespace MEDICSALLOCATION_2.Models;

public static class MedicineImageCatalog
{
    public static string? GetPath(string? medicineName) => medicineName switch
    {
        "Paracetamol 500mg" => "/images/medicines/paracetamol-500mg.png",
        "Salbutamol Inhaler" => "/images/medicines/salbutamol-inhaler.png",
        "Metformin 500mg" => "/images/medicines/metformin-500mg.png",
        "Insulin (Soluble)" => "/images/medicines/insulin-actrapid.png",
        "Cotrimoxazole 960mg" => "/images/medicines/cotrimoxazole-960mg.png",
        "Amoxicillin 500mg" => "/images/medicines/amoxicillin-500mg.png",
        "Amlodipine 5mg" => "/images/medicines/amlodipine-5mg.png",
        _ => null
    };
}
