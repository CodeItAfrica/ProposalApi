namespace GibsLifesMicroWebApi.Contracts.V1
{

    public enum GenderEnum
    {
        MALE,
        FEMALE
    }

    public enum KycTypeEnum
    {
        //[GibsValue("OTHERS", "NONE", "")]
        NOT_AVAILABLE,
        //[GibsValue("DRIVER LICENSE", "DRIVERS LICENSE")]
        DRIVERS_LICENSE,
        //[GibsValue("INTL. PASSPORT")]
        PASSPORT,
        //[GibsValue("NATIONAL ID CARD")]
        NATIONAL_ID_CARD,
        //[GibsValue("RC NO.")]
        COMPANY_REG_NO
    }

}
