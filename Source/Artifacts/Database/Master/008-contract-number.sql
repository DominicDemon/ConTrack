IF OBJECT_ID(N'dbo.CONTRACT_NUMBER_COUNTER', N'U') IS NULL
    CREATE TABLE dbo.CONTRACT_NUMBER_COUNTER (
        contract_year int NOT NULL PRIMARY KEY,
        last_number bigint NOT NULL
    );
GO
CREATE OR ALTER PROCEDURE dbo.AllocateContractNumber
    @contract_year int
AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT = 0 THROW 50000, 'A contract transaction is required.', 1;

    UPDATE dbo.CONTRACT_NUMBER_COUNTER WITH (UPDLOCK, HOLDLOCK)
    SET last_number = last_number + 1
    WHERE contract_year = @contract_year;

    IF @@ROWCOUNT = 0
        INSERT dbo.CONTRACT_NUMBER_COUNTER (contract_year, last_number) VALUES (@contract_year, 1);

    SELECT last_number AS [Value] FROM dbo.CONTRACT_NUMBER_COUNTER WHERE contract_year = @contract_year;
END;
GO
