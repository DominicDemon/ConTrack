IF OBJECT_ID('dbo.CONTRACT_NUMBER_RULE','U') IS NULL
    CREATE TABLE dbo.CONTRACT_NUMBER_RULE (
        owner_id bigint PRIMARY KEY, -- 0 is the issuing company; other IDs identify CUSTOMER_BUSINESS.
        CompanyCode varchar(16) NOT NULL UNIQUE,
        TypeCode varchar(10) NOT NULL,
        IncludeDay bit NOT NULL
    );
IF OBJECT_ID('dbo.BUSINESS_NUMBER_COUNTER','U') IS NULL
    CREATE TABLE dbo.BUSINESS_NUMBER_COUNTER (
        owner_id bigint NOT NULL REFERENCES dbo.CONTRACT_NUMBER_RULE(owner_id),
        contract_year int NOT NULL, last_number bigint NOT NULL,
        PRIMARY KEY(owner_id,contract_year)
    );
GO
CREATE OR ALTER PROCEDURE dbo.AllocateFormattedContractNumber @owner bigint, @day date
AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT=0 THROW 50000,'Allocation requires transaction',1;
    DECLARE @year int=YEAR(@day), @prefix varchar(40), @n bigint, @code varchar(50);
    SELECT @prefix=CompanyCode+'-'+TypeCode+'-'+LEFT(CONVERT(char(8),@day,112),CASE WHEN IncludeDay=1 THEN 8 ELSE 6 END)+'-'
        FROM dbo.CONTRACT_NUMBER_RULE WITH (HOLDLOCK) WHERE owner_id=@owner;
    IF @prefix IS NULL AND @owner<>0 THROW 50000,'Missing company rule',1;
    SET @prefix=COALESCE(@prefix,'HD-'+CONVERT(char(8),@day,112)+'-');
    DECLARE @issued TABLE(n bigint);
    WHILE 1=1
    BEGIN
        IF @owner=0
        BEGIN
            DELETE FROM @issued;
            INSERT INTO @issued EXEC dbo.AllocateContractNumber @year;
            SELECT @n=n FROM @issued;
        END
        ELSE
        BEGIN
            UPDATE dbo.BUSINESS_NUMBER_COUNTER WITH (UPDLOCK,HOLDLOCK)
              SET @n=last_number=last_number+1 WHERE owner_id=@owner AND contract_year=@year;
            IF @@ROWCOUNT=0
            BEGIN
                SET @n=1;
                INSERT dbo.BUSINESS_NUMBER_COUNTER VALUES (@owner,@year,@n);
            END
        END
        DECLARE @digits varchar(20)=CONVERT(varchar(20),@n);
        IF LEN(@prefix)+LEN(@digits)>50 THROW 50000,'Contract number too long',1;
        SET @code=@prefix+CASE WHEN LEN(@digits)<6 THEN RIGHT('000000'+@digits,6) ELSE @digits END;
        IF NOT EXISTS(SELECT 1 FROM dbo.CONTRACT_DOCUMENTS WHERE contract_document_code=@code) BREAK;
    END
    SELECT @code AS Value;
END;
GO
