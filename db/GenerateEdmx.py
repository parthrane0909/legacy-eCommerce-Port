# -*- coding: utf-8 -*-
"""Generate a complete EDMX for LegacyEcommerceDb matching the runtime model."""
import xml.dom.minidom as minidom
import xml.etree.ElementTree as ET

IMPORT_EF = 'http://schemas.microsoft.com/ado/2009/11/edm'
SSDL_NS = 'http://schemas.microsoft.com/ado/2009/11/edm/ssdl'
MAPPING_NS = 'http://schemas.microsoft.com/ado/2008/09/mapping'

CATALOG_NS = 'Ecommerce.Core.Entities'
STORE_NS = 'LegacyEcommerceModel.Store'

ENTITIES = {
 "Category": [("Id","Int32",False,True),("Name","String",False,False),("Slug","String",False,False),("Description","String",True,False),("IsActive","Boolean",False,False),("SortOrder","Int32",False,False),("ParentId","Int32",True,False)],
 "Product": [("Id","Int32",False,True),("Name","String",False,False),("Slug","String",False,False),("Description","String",True,False),("Price","Decimal",False,False),("CategoryId","Int32",False,False),("IsFeatured","Boolean",False,False),("IsActive","Boolean",False,False),("CreatedAt","DateTime",False,False),("DiscountPercent","Int32",True,False),("RatingNum","Decimal",True,False),("Stock","Int32",True,False)],
 "ProductImage": [("Id","Int32",False,True),("ProductId","Int32",False,False),("Url","String",False,False),("AltText","String",True,False),("IsPrimary","Boolean",False,False),("SortOrder","Int32",False,False)],
 "ProductVariant": [("Id","Int32",False,True),("ProductId","Int32",False,False),("Name","String",False,False),("Sku","String",True,False),("PriceDelta","Decimal",False,False),("Stock","Int32",False,False)],
 "CartItem": [("Id","Int32",False,True),("SessionId","String",False,False),("ProductId","Int32",False,False),("VariantId","Int32",True,False),("ProductName","String",False,False),("VariantName","String",True,False),("ImageUrl","String",True,False),("UnitPrice","Decimal",False,False),("Quantity","Int32",False,False)],
 "Order": [("Id","Int32",False,True),("OrderNumber","String",False,False),("CustomerId","String",False,False),("Status","String",False,False),("Subtotal","Decimal",False,False),("ShippingCost","Decimal",False,False),("Total","Decimal",False,False),("ShipFullName","String",True,False),("ShipLine1","String",True,False),("ShipCity","String",True,False),("ShipState","String",True,False),("ShipPostalCode","String",True,False),("ShipCountry","String",True,False),("ShipPhone","String",True,False),("ShippingMethod","String",True,False),("PaymentMethod","String",True,False),("CreatedAt","DateTime",False,False)],
  "OrderLine": [("Id","Int32",False,True),("OrderId","Int32",False,False),("ProductId","Int32",False,False),("ProductName","String",False,False),("VariantName","String",True,False),("UnitPrice","Decimal",False,False),("Quantity","Int32",False,False),("LineTotal","Decimal",False,False)],
 "Address": [("Id","Int32",False,True),("CustomerId","String",False,False),("FullName","String",True,False),("Line1","String",True,False),("Line2","String",True,False),("City","String",True,False),("State","String",True,False),("PostalCode","String",True,False),("Country","String",True,False),("Phone","String",True,False),("IsDefault","Boolean",False,False)],
}
SETS = {k: {"setname": v[1] if isinstance(v, tuple) else None for v in [i for i in []]} for k,v in {}.items() if False}
# entity set names
ESET = {"Category":"Categories","Product":"Products","ProductImage":"ProductImages","ProductVariant":"ProductVariants","CartItem":"CartItems","Order":"Orders","OrderLine":"OrderLines","Address":"Addresses"}
# SSDL types
SSDL_TYPES = {"Int32":"int","String":"nvarchar(max)","Boolean":"bit","DateTime":"datetime","Decimal":"decimal"}

# Associations: (name, principal_entity, principal_role, dependent_entity, dependent_role)
ASSOCS = [
  ("Category_Products",   "Category", "Category", "Product", "Products"),
  ("Category_Children",   "Category", "Parent",   "Category", "Children"),
  ("Product_Images",      "Product",  "Product",  "ProductImage", "Images"),
  ("Product_Variants",    "Product",  "Product",  "ProductVariant", "Variants"),
  ("Order_OrderLines",    "Order",    "Order",    "OrderLine",      "Lines"),
]
FKCOL = {"Category_Products":"CategoryId","Category_Children":"ParentId","Product_Images":"ProductId","Product_Variants":"ProductId","Order_OrderLines":"OrderId"}

def ssdl_scalar_col(k, t):
    attrs={"Name":k, "Type":SSDL_TYPES[t]}
    return attrs

schema = ET.Element("Schema", {"Namespace": STORE_NS, "Provider": "System.Data.SqlClient", "ProviderManifestToken":"2012"})
schema.set("xmlns", SSDL_NS)
for ename, cols in ENTITIES.items():
    et = ET.SubElement(schema, "EntityType", {"Name": ename})
    key = ET.SubElement(et, "Key")
    ET.SubElement(key, "PropertyRef", {"Name": cols[0][0]})
    for name,t,nullable, identity in cols:
        attrs = {"Name": name, "Type": SSDL_TYPES[t]}
        if not nullable: attrs["Nullable"]="false"
        if t=="Decimal": attrs["Precision"]="18"; attrs["Scale"]="2"
        p = ET.SubElement(et, "Property", attrs)
        if identity: p.set("StoreGeneratedPattern","Identity")

ec = ET.SubElement(schema, "EntityContainer", {"Name": "StoreContainer"})
for ename, cset in ESET.items():
    ET.SubElement(ec, "EntitySet", {"Name": cset, "EntityType": f"{STORE_NS}.{ename}", "Schema":"dbo","Table": cset})
# SSDL associations
for an, p_ent, pr, d_ent, dr in ASSOCS:
    a = ET.SubElement(schema, "Association", {"Name": an})
    ET.SubElement(a, "End", {"Role": pr, "Type": f"{STORE_NS}.{p_ent}", "Multiplicity":("0..1" if an=="Category_Children" else "1")})
    ET.SubElement(a, "End", {"Role": dr, "Type": f"{STORE_NS}.{d_ent}", "Multiplicity":"*"})
    rc = ET.SubElement(a, "ReferentialConstraint")
    pr_el = ET.SubElement(rc, "Principal", {"Role": pr}); ET.SubElement(pr_el, "PropertyRef", {"Name": "Id"})
    dp_el = ET.SubElement(rc, "Dependent", {"Role": dr}); ET.SubElement(dp_el, "PropertyRef", {"Name": FKCOL[an]})
    # async set handled in csdl? association set goes to EntityContainer
    as_el = ET.SubElement(ec, "AssociationSet", {"Name": an, "Association": f"{STORE_NS}.{an}"})
    ET.SubElement(as_el,"End",{"Role":pr,"EntitySet":ESET[p_ent]})
    ET.SubElement(as_el,"End",{"Role":dr,"EntitySet":ESET[d_ent]})

# CSDL
cschema = ET.Element("Schema", {"Namespace": CATALOG_NS, "xmlns": IMPORT_EF})
for ename, cols in ENTITIES.items():
    et = ET.SubElement(cschema, "EntityType", {"Name": ename})
    key = ET.SubElement(et, "Key")
    ET.SubElement(key, "PropertyRef", {"Name": cols[0][0]})
    for name,t,nullable,identity in cols:
        attrs={"Name":name,"Type":f"Edm.{t}"}
        if not nullable: attrs["Nullable"]="false"
        ET.SubElement(et, "Property", attrs)
    for an, p_ent, pr, d_ent, dr in ASSOCS:
        if p_ent==ename and dr!="Products": pass
    # navigation metadata added later per-entity
# add navigation for convenience below per-entity edges
nav_by_name = {"Category_Products":("Products", "ToRole"), "Category_Children":("Children", "ToRole"), "Product_Images":("Images", "ToRole"), "Product_Variants":("Variants","ToRole"), "Order_OrderLines":("Lines","ToRole")}
# per-entity nav roles:
# Category: Products, Children  ; Product: Category (FromRole Products ToRole Category), Images,Variants ; ProductImage: Product; ProductVariant: Product ; Order: Lines ; OrderLine: Order
NAV_SPECS = {
 "Category": [("Products","Category_Products","Category","Products"), ("Children","Category_Children","Parent","Children"), ("ProductsOfParent","Category_Children","Children","Parent")],
 "Product": [("Category","Category_Products","Products","Category"), ("Images","Product_Images","Product","Images"), ("Variants","Product_Variants","Product","Variants")],
 "ProductImage": [("Product","Product_Images","Images","Product")],
 "ProductVariant": [("Product","Product_Variants","Variants","Product")],
 "Order": [("Lines","Order_OrderLines","Order","Lines")],
 "OrderLine": [("Order","Order_OrderLines","Lines","Order")],
 "Category_Children": None
}
# remove bogus key
# Note: Category->NoReverseNav for Parent? provide only forward nav 'Parent'
NAV = {
 "Category": [("Products","Category_Products","Category","Products"), ("Children","Category_Children","Parent","Children")],
 "Product": [("Category","Category_Products","Products","Category"), ("Images","Product_Images","Product","Images"), ("Variants","Product_Variants","Product","Variants")],
 "ProductImage": [("Product","Product_Images","Images","Product")],
 "ProductVariant": [("Product","Product_Variants","Variants","Product")],
 "Order": [("Lines","Order_OrderLines","Order","Lines")],
 "OrderLine": [("Order","Order_OrderLines","Lines","Order")],
 "Category_Back_Children_Parent": None,
}
for ename, navs in NAV.items():
    if not navs or ename.startswith("Category_"): continue
    et = None
    for e in cschema.findall("EntityType"):
        if e.get("Name")==ename: et = e; break
    for nname, aref, fromr, tor in navs:
        # nav appears in entity type containing the OWNing side
        pass
# Simpler approach: add nav properties directly CSE
for e in cschema.findall("EntityType"):
    ename = e.get("Name")
    if ename=="Category":
        add_nav = True
        navs = [("Products","Category_Products","Category","Products"), ("Children","Category_Children","Parent","Children")]
    elif ename=="Product":
        navs = [("Category","Category_Products","Products","Category"), ("Images","Product_Images","Product","Images"), ("Variants","Product_Variants","Product","Variants")]
    elif ename in ("ProductImage",):
        navs = [("Product","Product_Images","Images","Product")]
    elif ename in ("ProductVariant",):
        navs = [("Product","Product_Variants","Variants","Product")]
    elif ename=="Order":
        navs = [("Lines","Order_OrderLines","Order","Lines")]
    elif ename=="OrderLine":
        asm = [("Order","Order_OrderLines","Lines","Order")]
        navs=asm
    elif ename=="Category":
        navs = [("Products","Category_Products","Category","Products"), ("Children","Category_Children","Parent","Children")]
    else:
        navs=[]
    for nname, aref, fr, to in navs:
        ET.SubElement(e, "NavigationProperty", {"Name": nname, "Relationship": f"{CATALOG_NS}.{aref}", "FromRole": fr, "ToRole": to})

# association definitions in CSDL
for an, p_ent, pr, d_ent, dr in ASSOCS:
    a = ET.SubElement(cschema, "Association", {"Name": an})
    ET.SubElement(a,"End",{"Role":pr,"Type":f"{CATALOG_NS}.{p_ent}","Multiplicity":("0..1" if an=="Category_Children" else "1")})
    ET.SubElement(a,"End",{"Role":dr,"Type":f"{CATALOG_NS}.{d_ent}","Multiplicity":"*"})
    rc = ET.SubElement(a, "ReferentialConstraint")
    rp = ET.SubElement(rc, "Principal", {"Role": pr}); ET.SubElement(rp, "PropertyRef", {"Name": "Id"})
    rd = ET.SubElement(rc, "Dependent", {"Role": dr}); ET.SubElement(rd, "PropertyRef", {"Name": FKCOL[an]})
ec = ET.SubElement(cschema, "EntityContainer", {"Name": "EcommerceModel"})
for ename, cset in ESET.items():
    ET.SubElement(ec, "EntitySet", {"Name": cset, "EntityType": f"{CATALOG_NS}.{ename}"})
for an, p_ent, pr, d_ent, dr in ASSOCS:
    ac = ET.SubElement(ec, "AssociationSet", {"Name": an, "Association": f"{CATALOG_NS}.{an}"})
    ET.SubElement(ac, "End", {"Role": pr, "EntitySet": ESET[p_ent]})
    ET.SubElement(ac, "End", {"Role": dr, "EntitySet": ESET[d_ent]})

# MSL
using_ns = "http://schemas.microsoft.com/ado/2008/09/mapping"
m_root = ET.Element("Mapping", {"Space":"C-S", "xmlns":"http://schemas.microsoft.com/ado/2009/11/mapping/cs"})
mapper = ET.SubElement(m_root, "EntityContainerMapping", {"StorageEntityContainer":"StoreContainer","CdmEntityContainer":"EcommerceModel"})
for ename, cset in ESET.items():
    eSet = ET.SubElement(mapper, "EntitySetMapping", {"Name": cset})
    etm = ET.SubElement(eSet, "EntityTypeMapping", {"TypeName": f"{CATALOG_NS}.{ename}"})
    frag = ET.SubElement(etm, "MappingFragment", {"StoreEntitySet": cset})
    for name,t,nullable,identity in ENTITIES[ename]:
        ET.SubElement(frag, "ScalarProperty", {"Name":name,"ColumnName":name})
# Like EF EdmxWriter for FK associations: no explicit AssociationSetMapping;
# EF manufactures FK-association views from referential constraints automatically.

# assemble edmx
root = ET.Element("edmx:Edmx", {"Version":"3.0", "xmlns:edmx":"http://schemas.microsoft.com/ado/2009/11/edmx"})
runtime = ET.SubElement(root, "edmx:Runtime")
cms = ET.SubElement(runtime, "edmx:ConceptualModels")
cms.append(import_csdl := cschema)
sms = ET.SubElement(runtime, "edmx:StorageModels")
sms.append(schema)
maps = ET.SubElement(runtime, "edmx:Mappings")
maps.append(m_root)

# Add xmlns/xsi:schemaLocation
sd = minidom.parseString(ET.tostring(root)).toxml(encoding='utf-8').decode()
print(sd[:400])
open('Ecommerce.Data/LegacyEcommerce.edmx','w').write(sd)
print("written")
