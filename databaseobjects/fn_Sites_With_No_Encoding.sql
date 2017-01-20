USE [MIS]
GO

/****** Object:  UserDefinedFunction [dbo].[fn_Sites_With_No_Encoding]    Script Date: 1/20/2017 10:31:41 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[fn_Sites_With_No_Encoding](@min_term CHAR(5), @max_term CHAR(5))
RETURNS @sites TABLE
(
	centercode VARCHAR(MAX)
	,sitename VARCHAR(MAX)
	,terms VARCHAR(MAX)
	,classesencoded INT
)
AS
BEGIN
	;WITH campuscenters as (SELECT
		cntr.CampusCode + cntr.LocationNumber AS 'Center Code'
		,cntr.LocationName
	FROM
		MIS.dbo.vwCampusCenter cntr)

	INSERT INTO @sites
		SELECT
			center.[Center Code]
			,center.LocationName AS 'Site Name'
			,@min_term + '-' + @max_term AS 'Term(s)'
			,SUM(CASE WHEN class.ISN_ST_CLASS_A IS NOT NULL THEN 1 ELSE 0 END) AS 'Classes Encoded'
		FROM
			campuscenters center
			LEFT JOIN MIS.dbo.ST_CLASS_A_151 class ON center.[Center Code] = class.campCntr
													AND class.effTrm >= @min_term
													AND class.effTrm <= @max_term
		GROUP BY
			center.[Center Code], center.LocationName
		HAVING
			SUM(CASE WHEN class.ISN_ST_CLASS_A IS NOT NULL THEN 1 ELSE 0 END) = 0

		RETURN
END

GO


