using System.Xml.Linq;

namespace KpicCafeteria.Documents.Hwpx;

/// <summary>
/// HWPX XML 네임스페이스 상수.
/// 기존 hwpx_engine.py HWPX_NAMESPACES에 대응.
/// </summary>
public static class HwpxNamespaces
{
    public const string Opf = "http://www.idpf.org/2007/opf/";
    public const string Ha = "http://www.hancom.co.kr/hwpml/2011/app";
    public const string Hp = "http://www.hancom.co.kr/hwpml/2011/paragraph";
    public const string Hs = "http://www.hancom.co.kr/hwpml/2011/section";
    public const string Hc = "http://www.hancom.co.kr/hwpml/2011/core";
    public const string Hh = "http://www.hancom.co.kr/hwpml/2011/head";

    public static XNamespace OpfNs => Opf;
    public static XNamespace HpNs => Hp;
    public static XNamespace HsNs => Hs;
}
