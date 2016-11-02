using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PLR;
using System.Data.SqlClient;
using CommandLine;
using CommandLine.Text;
using System.IO;

namespace CDM
{
    class CDMReport
    {
        class Options
        {
            [Option('m', "minTerm", Required = true,
                HelpText = "Term after which classes taking place and effective programs will qualify")]
            public String minTerm { get; set; }
            [Option('x', "maxTerm", Required = true,
                HelpText = "Term before which classes taking place and effective programs will qualify")]
            public String maxTerm { get; set; }
            [Option('p', "programCode", Required = false, DefaultValue = "",
                HelpText = "Used to restrict results to classes for a specific program (not required)")]
            public String programCode { get; set; }
            [Option('c', "campusCenter", Required = false, DefaultValue = "",
                HelpText = "Used to restrict results to classes taking place a specific campus Center (not required)")]
            public String campusCenter { get; set; }
            [Option('a', "awardType", Required = false, DefaultValue = "",
                HelpText = "Used to restrict results to classes for programs with the specified award type")]
            public String awardType { get; set; }
            [Option('r', "highSchoolMode", Required = false, DefaultValue = false,
                HelpText = "Used to toggle high school mode on or off (causes only high school campus centers to be returned, default = false)")]
            public bool runForHighSchool { get; set; }
            [Option('o', "month", Required = true)]
            public String month { get; set; }
            [Option('y', "year", Required = true)]
            public String year { get; set; }
            [ParserState]
            public IParserState LastParserState { get; set; }

            [HelpOption]
            public string GetUsage()
            {
                return HelpText.AutoBuild(this,
                  (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
            }
        }

        static void Main(string[] args)
        {
            Options options = new Options();
            Parser.Default.ParseArguments(args, options);

            //derived parameters
            int minTermYear = int.Parse(options.minTerm.Substring(0, 4));
            int minTermTerm = int.Parse("" + options.minTerm[4]);
            int maxTermYear = int.Parse(options.maxTerm.Substring(0, 4));
            int maxTermTerm = int.Parse("" + options.maxTerm[4]);

            //datastores
            Dictionary<String, AcademicProgram> programDictionary = new Dictionary<string, AcademicProgram>();
            Dictionary<String, Course> globalCourseDictionary = new Dictionary<string, Course>();
            Dictionary<String, List<String>> onlineCourses = new Dictionary<string, List<string>>();
            Dictionary<String, List<String>> AAElectivesByTerm = new Dictionary<string, List<string>>();
            Dictionary<Tuple<String, String>, float> TotalProgramHoursForProgram = new Dictionary<Tuple<string, string>, float>();
            Dictionary<Tuple<String, String>, float> TotalGeneralEducationHoursForProgram = new Dictionary<Tuple<string, string>, float>();
            Dictionary<Tuple<String, String>, float> TotalCoreAndProfessionalForProgram = new Dictionary<Tuple<string, string>, float>();
            List<AcademicProgram> programs = new List<AcademicProgram>(); 

            SqlConnection conn = new SqlConnection("Server=vulcan;database=MIS;Trusted_Connection=yes");

            try
            {
                conn.Open();
            }
            catch (Exception)
            {

                throw;
            }


            SqlCommand comm = new SqlCommand("SELECT DISTINCT                                                                                                                           "
	                                         +"       prog.PGM_CD                                                                                                                       "
                                             +"       ,CASE                                                                                                                             "                                                                                             
		                                     +"         WHEN prog.PGM_OFFCL_TTL <> '' THEN prog.PGM_OFFCL_TTL                                                                           "
		                                     +"         ELSE prog.PGM_TRK_TTL                                                                                                           "
	                                         +"   END AS [Title]                                                                                                                        "
	                                         +"       ,prog.AWD_TY                                                                                                                      "
	                                         +"       ,prog.EFF_TRM_D                                                                                                                   "
	                                         +"       ,prog.END_TRM                                                                                                                     "
	                                         +"       ,proggroup.PGM_AREA                                                                                                               "
	                                         +"       ,progarea.PGM_AREA_TYPE                                                                                                           "
	                                         +"       ,proggroup.PGM_AREA_GROUP                                                                                                         "
	                                         +"       ,proggroup.PGM_AREA_OPTN_CD                                                                                                       "
	                                         +"       ,proggroup.PGM_AREA_OPTN_OPER                                                                                                     "
	                                         +"       ,groupcourse.PGM_AREA_GROUP_CRS                                                                                                   "
	                                         +"       ,CASE WHEN prog.AWD_TY = 'VC' THEN prog.PGM_TTL_MIN_CNTCT_HRS_REQD ELSE prog.PGM_TTL_CRD_HRS END AS HRS                           "
	                                         +"       ,prog.PGM_TTL_GE_HRS_REQD                                                                                                         "
                                             +"       ,prog.FIN_AID_APPRVD                                                                                                              "
                                             +"   FROM                                                                                                                                  "
	                                         +"       MIS.dbo.ST_PROGRAMS_A_136  prog                                                                                                   "
	                                         +"       INNER JOIN MIS.dbo.ST_PROGRAMS_A_136 progarea ON progarea.PGM_CD = prog.PGM_CD                                                    "
											 +"	                                                  AND progarea.EFF_TRM_A = prog.EFF_TRM_D                                               "
	                                         +"       INNER JOIN MIS.dbo.ST_PROGRAMS_A_136 proggroup ON proggroup.PGM_CD = prog.PGM_CD                                                  "
											 +"	                                                   AND proggroup.EFF_TRM_G = prog.EFF_TRM_D                                             "
	                                         +"       INNER JOIN MIS.dbo.ST_PROGRAMS_A_PGM_AREA_GROUP_CRS_136 groupcourse ON groupcourse.ISN_ST_PROGRAMS_A = proggroup.ISN_ST_PROGRAMS_A"
                                             +"   WHERE                                                                                                                                 "
	                                         +"       prog.EFF_TRM_D <> ''                                                                                                              "
                                             +"       AND prog.EFF_TRM_D <= '" + options.maxTerm + "'                                                                                   "
                                             +"       AND prog.END_TRM = ''                                                                                                             "
	                                         +"       AND prog.AWD_TY NOT IN ('NC','ND','HS')                                                                                           "
	                                         +"       AND SUBSTRING(prog.PGM_CD, 1, 2) <> '00'                                                                                          "
                                             +"   ORDER BY                                                                                                                              "
	                                         +"       prog.PGM_CD                                                                                                                       "
	                                         +"       ,prog.EFF_TRM_D                                                                                                                   "
	                                         +"       ,proggroup.PGM_AREA                                                                                                               "
	                                         +"       ,proggroup.PGM_AREA_GROUP", conn);
            SqlDataReader reader = comm.ExecuteReader();

            while (reader.Read())
            {
                String curProgramCode = reader["PGM_CD"].ToString();
                String effectiveTerm = reader["EFF_TRM_D"].ToString();
                String endTerm = reader["END_TRM"].ToString();
                String courseID = reader["PGM_AREA_GROUP_CRS"].ToString().Trim();
                int areaNum = int.Parse(reader["PGM_AREA"].ToString());
                int groupNum = int.Parse(reader["PGM_AREA_GROUP"].ToString());
                bool financialAidApproved = reader["FIN_AID_APPRVD"].ToString() == "Y";

                AcademicProgram prog;

                if (!programDictionary.ContainsKey(curProgramCode))
                {
                    prog = new AcademicProgram();
                    prog.progCode = curProgramCode;
                    prog.awardType = reader["AWD_TY"].ToString();
                    prog.progName = reader["Title"].ToString();
                    prog.financialAidApproved = financialAidApproved;
                    prog.catalogChanges = new List<AcademicProgram.CatalogChange>();
                    prog.catalogDictionary = new Dictionary<string, AcademicProgram.CatalogChange>();
                    programs.Add(prog);
                    programDictionary.Add(curProgramCode, prog);
                }
                else
                {
                    prog = programDictionary[curProgramCode];
                }

                AcademicProgram.CatalogChange catalog;

                if (!prog.catalogDictionary.ContainsKey(effectiveTerm))
                {
                    catalog = new AcademicProgram.CatalogChange();
                    catalog.effectiveTerm = effectiveTerm;
                    catalog.endTerm = endTerm;
                    catalog.totalProgramHours = (int)float.Parse(reader["HRS"].ToString());
                    catalog.totalGeneralEducationHours = (int)float.Parse(reader["PGM_TTL_GE_HRS_REQD"].ToString());
                    catalog.totalCoreAndProfessionalHours = catalog.totalProgramHours - catalog.totalGeneralEducationHours;
                    catalog.effectiveTermYear = int.Parse(catalog.effectiveTerm.Substring(0, 4));
                    catalog.effectiveTermTerm = int.Parse("" + catalog.effectiveTerm[4]);
                    catalog.endTermYear = catalog.endTerm != "" ? int.Parse(catalog.endTerm.Substring(0, 4)) : 9999;
                    catalog.endTermTerm = catalog.endTerm != "" ? int.Parse("" + catalog.endTerm[4]) : 9;
                    catalog.areaDictionary = new Dictionary<int, AcademicProgram.CatalogChange.Area>();
                    catalog.flatCourseArray = new List<string>();
                    prog.catalogChanges.Add(catalog);
                    prog.catalogDictionary.Add(effectiveTerm, catalog);
                    catalog.areas = new List<AcademicProgram.CatalogChange.Area>();
                }
                else
                {
                    catalog = prog.catalogDictionary[effectiveTerm];
                }

                AcademicProgram.CatalogChange.Area area;

                if (!catalog.areaDictionary.ContainsKey(areaNum))
                {
                    area = new AcademicProgram.CatalogChange.Area();
                    area.areaNum = areaNum;
                    area.areaType = reader["PGM_AREA_TYPE"].ToString();
                    area.groupDictionary = new Dictionary<int, AcademicProgram.CatalogChange.Area.Group>();
                    area.groups = new List<AcademicProgram.CatalogChange.Area.Group>();
                    catalog.areas.Add(area);
                    catalog.areaDictionary.Add(area.areaNum, area);
                }
                else
                {
                    area = catalog.areaDictionary[areaNum];
                }

                AcademicProgram.CatalogChange.Area.Group group;

                if (!area.groupDictionary.ContainsKey(groupNum))
                {

                    group = new AcademicProgram.CatalogChange.Area.Group();
                    group.groupNum = groupNum;
                    group.optCode = reader["PGM_AREA_OPTN_CD"].ToString();
                    group.operatorCode = reader["PGM_AREA_OPTN_OPER"].ToString();
                    group.courseDictionary = new Dictionary<string, Course>();
                    group.courses = new List<Course>();
                    area.groups.Add(group);
                    area.groupDictionary.Add(groupNum, group);
                }
                else
                {
                    group = area.groupDictionary[groupNum];
                }

                Course course;

                if (!globalCourseDictionary.ContainsKey(courseID))
                {
                    course = new Course();
                    course.courseID = courseID;
                    globalCourseDictionary.Add(courseID, course);
                }
                else
                {
                    course = globalCourseDictionary[courseID];
                }

                if (group.courseDictionary.ContainsKey(courseID))
                {
                    continue;
                }
                group.courseDictionary.Add(courseID, course);
                group.courses.Add(course);
                catalog.flatCourseArray.Add(courseID);

            }

            reader.Close();

            comm = new SqlCommand("SELECT                                                                                                                           "  
                                 +"     class.crsID                                                                                                                 " 
                                 +"     ,class.efftrm                                                                                                               " 
                                 +"     ,CASE                                                                                                                       " 
                                 +"         WHEN course.CRED_TY IN ('01','02','03','14','15') THEN class.EVAL_CRED_HRS                                              " 
                                 +"         ELSE class.CNTCT_HRS                                                                                                    " 
                                 +"     END AS HRS                                                                                                                  " 
                                 +"     ,course.USED_FOR_AA_ELECTIVE                                                                                                " 
                                 +"     ,CASE                                                                                                                       " 
                                 +"         WHEN SUM(CASE                                                                                                           " 
                                 +"                 WHEN class.NON_FF_PERC > 50 THEN 1                                                                              " 
                                 +"                 ELSE 0                                                                                                          " 
                                 +"             END) > 1 THEN 'Online'                                                                                              " 
                                 +"         ELSE 'Not Online'                                                                                                       " 
                                 +"     END AS 'Online_Status'                                                                                                      "                                                                                        
                                 +" FROM                                                                                                                            " 
                                 +"     MIS.dbo.ST_CLASS_A_151 class                                                                                                " 
                                 +"     INNER JOIN MIS.dbo.ST_COURSE_A_150 course ON course.CRS_ID = class.crsID                                                    " 
                                 +"     INNER JOIN MIS.[dbo].[ST_PROGRAMS_A_PGM_AREA_GROUP_CRS_136] groupcourse ON groupcourse.[PGM_AREA_GROUP_CRS] =  course.CRS_ID" 
                                 +"     INNER JOIN MIS.dbo.ST_PROGRAMS_A_136 proggroup ON proggroup.ISN_ST_PROGRAMS_A = groupcourse.ISN_ST_PROGRAMS_A               " 
                                 +"     INNER JOIN MIS.dbo.ST_PROGRAMS_A_136 prog ON prog.PGM_CD = proggroup.PGM_CD                                                 "
		                         +"                                             AND prog.EFF_TRM_D = proggroup.EFF_TRM_G										    "   
                                 +" WHERE                                                                                                                           " 
                                 +"     class.efftrm >= '" + options.minTerm + "'                                                                                   "
                                 +"     AND class.efftrm <= '" + options.maxTerm + "'                                                                               "
                                 +"     AND prog.FIN_AID_APPRVD = 'Y'                                                                                               " 
                                 +"     AND prog.AWD_TY NOT IN ('NC','ND','HS')                                                                                     " 
                                 +"     AND SUBSTRING(prog.PGM_CD, 1, 2) <> '00'                                                                                    " 
                                 +" GROUP BY                                                                                                                        " 
                                 +"     class.crsId                                                                                                                 " 
                                 +"     ,class.effTrm                                                                                                               " 
                                 +"     ,course.CRED_TY                                                                                                             " 
                                 +"     ,class.EVAL_CRED_HRS                                                                                                        " 
                                 +"     ,course.USED_FOR_AA_ELECTIVE                                                                                                " 
                                 +"     ,class.CNTCT_HRS", conn);
            comm.CommandTimeout = 240;
            reader = comm.ExecuteReader();

            while (reader.Read())
            {
                String course = reader["crsID"].ToString().Trim();
                String term = reader["efftrm"].ToString();
                float hours = float.Parse(reader["HRS"].ToString());
                bool online = reader["Online_Status"].ToString() == "Online";
                bool AAElective = reader["USED_FOR_AA_ELECTIVE"].ToString() == "Y";

                if (!globalCourseDictionary.ContainsKey(course))
                {
                    continue;
                }
                globalCourseDictionary[course].hours = hours;

                
                if (AAElective)
                {
                    if (!AAElectivesByTerm.ContainsKey(term))
                    {
                        AAElectivesByTerm.Add(term, new List<string>());
                    }
                    if (!AAElectivesByTerm[term].Contains(course))
                    {
                        AAElectivesByTerm[term].Add(course);
                    }
                }

                if (!onlineCourses.ContainsKey(term))
                {
                    onlineCourses.Add(term, new List<String>());
                }

                if (online && !onlineCourses[term].Contains(course))
                {
                    onlineCourses[term].Add(course);
                }
            }

            reader.Close();
            conn.Close();

            foreach (AcademicProgram curProgram in programs)
            {
                int maxCatalogYear = 0;
                int maxCatalogTerm = 0;
                AcademicProgram.CatalogChange maxCatalog = null;

                foreach (AcademicProgram.CatalogChange catalog in curProgram.catalogChanges)
                {
                    if (catalog.effectiveTermYear > maxCatalogYear && catalog.effectiveTermTerm > maxCatalogTerm)
                    {
                        maxCatalog = catalog;
                        maxCatalogYear = catalog.effectiveTermYear;
                        maxCatalogTerm = catalog.effectiveTermTerm;
                    }
                }

                AcademicProgram.CatalogChange[] copyOfCatalogChanges = new AcademicProgram.CatalogChange[curProgram.catalogChanges.Count];
                
                curProgram.catalogChanges.CopyTo(copyOfCatalogChanges);

                foreach (AcademicProgram.CatalogChange catalog in copyOfCatalogChanges)
                {
                    if (catalog.effectiveTerm != maxCatalog.effectiveTerm)
                    {
                        curProgram.catalogChanges.Remove(catalog);
                    }
                }
            }

            foreach (AcademicProgram curProgram in programs)
            {
                foreach (AcademicProgram.CatalogChange catalog in curProgram.catalogChanges)
                {
                    float totalProgramHours = 0;
                    float totalGenEdHours = 0;
                    float totalCoreAndProfessionalHours = 0;

                    int curYear = catalog.effectiveTermYear;
                    int curTerm = catalog.effectiveTermTerm;

                    List<String> satisfiedCourses = new List<string>();
                    List<String> AACoursesForProgram = new List<string>();

                    Tuple<String, String> key = new Tuple<string, string>(curProgram.progCode, catalog.effectiveTerm);
                    TotalProgramHoursForProgram.Add(key, 0);
                    TotalGeneralEducationHoursForProgram.Add(key, 0);
                    TotalCoreAndProfessionalForProgram.Add(key, 0);
                       
                    while ((curYear < maxTermYear || (curYear == maxTermYear && curTerm <= maxTermTerm))
                        && (curYear < catalog.endTermYear || (curYear == catalog.endTermYear && curTerm <= catalog.endTermTerm)))
                    {
                        String term = curYear.ToString() + curTerm.ToString();

                        if (!onlineCourses.ContainsKey(term))
                        {
                            curYear = curTerm == 3 ? curYear + 1 : curYear;
                            curTerm = curTerm == 3 ? 1 : curTerm + 1;
                            continue;
                        }
                        foreach (String course in catalog.flatCourseArray)
                        {
                            if (onlineCourses[term].Contains(course))
                            {
                                satisfiedCourses.Add(course);
                            }
                        }

                        if (curProgram.progCode == "1108" )
                        {
                            if (AAElectivesByTerm.ContainsKey(term))
                            {
                               foreach (String course in AAElectivesByTerm[term])
                                {
                                    if (!AACoursesForProgram.Contains(course))
                                    {
                                        AACoursesForProgram.Add(course);
                                        totalProgramHours += globalCourseDictionary[course].hours;
                                        totalCoreAndProfessionalHours += globalCourseDictionary[course].hours;
                                    }
                                }  
                            }
                           
                        }

                        curYear = curTerm == 3 ? curYear + 1 : curYear;
                        curTerm = curTerm == 3 ? 1 : curTerm + 1;
                    }

                    foreach (AcademicProgram.CatalogChange.Area area in catalog.areas)
                    {
                        bool genEdArea = false;

                        switch (area.areaType.Substring(0, 2))
                        {
                            case "01":
                            case "02":
                            case "03":
                            case "04":
                            case "05":
                                if (curProgram.awardType != "VC" && curProgram.awardType != "TC")
                                {
                                    genEdArea = true;
                                }

                                break;
                            default:
                                if (curProgram.awardType == "AA")
                                {
                                    genEdArea = true;
                                }

                                break;
                        }

                        List<float> groupHours = new List<float>();
                        List<char> operands = new List<char>();
                        List<float> andGroups = new List<float>();

                        foreach (AcademicProgram.CatalogChange.Area.Group group in area.groups)
                        {
                            float groupHoursTotal = 0;

                            List<Course> coursesInGroup = new List<Course>();

                            foreach (String course in group.courseDictionary.Keys)
                            {
                                if (satisfiedCourses.Contains(course))
                                {
                                    coursesInGroup.Add(group.courseDictionary[course]);
                                }
                            }

                            switch (group.optCode)
                            {
                                case "14":
                                case "00":
                                    foreach (Course course in coursesInGroup)
                                    {
                                        groupHoursTotal += course.hours;
                                    }
                                    break;
                                case "11":

                                    float hoursForGroup = Course.findAndSumFirstNCourses(coursesInGroup, 2);
                                    groupHoursTotal += hoursForGroup;

                                    break;
                                case "21":
                                case "31":
                                case "41":
                                case "61":
                                case "71":
                                case "81":
                                case "82":
                                case "83":
                                case "84":

                                    hoursForGroup = Course.findAndSumFirstNCourses(coursesInGroup, 1);
                                    groupHoursTotal += hoursForGroup;

                                    break;
                                case "22":
                                case "32":
                                case "42":
                                case "62":
                                case "72":

                                    hoursForGroup = Course.findAndSumFirstNCourses(coursesInGroup, 2);
                                    groupHoursTotal += hoursForGroup;

                                    break;
                                case "23":
                                case "33":
                                case "43":
                                case "63":

                                    hoursForGroup = Course.findAndSumFirstNCourses(coursesInGroup, 3);
                                    groupHoursTotal += hoursForGroup;

                                    break;
                                case "24":
                                case "34":

                                    hoursForGroup = Course.findAndSumFirstNCourses(coursesInGroup, 4);
                                    groupHoursTotal += hoursForGroup;

                                    break;
                                case "25":

                                    hoursForGroup = Course.findAndSumFirstNCourses(coursesInGroup, 5);
                                    groupHoursTotal += hoursForGroup;

                                    break;
                                case "26":
                                case "87":

                                    hoursForGroup = Course.findAndSumFirstNCourses(coursesInGroup, 6);
                                    groupHoursTotal += hoursForGroup;

                                    break;
                                case "27":

                                    hoursForGroup = Course.findAndSumFirstNCourses(coursesInGroup, 7);
                                    groupHoursTotal += hoursForGroup;

                                    break;
                                case "28":

                                    hoursForGroup = Course.findAndSumFirstNCourses(coursesInGroup, 8);
                                    groupHoursTotal += hoursForGroup;

                                    break;
                                case "29":

                                    hoursForGroup = Course.findAndSumFirstNCourses(coursesInGroup, 9);
                                    groupHoursTotal += hoursForGroup;

                                    break;
                                default:
                                    break;
                            }

                            groupHours.Add(groupHoursTotal);
                            operands.Add(group.operatorCode.Length == 0 ? ' ' : group.operatorCode[0]);
                        }

                        for (int i = 0; i < operands.Count; i++)
                        {
                            float andGroupTotal = groupHours[0];
                            int j = i + 1;

                            do
                            {
                                if (j >= operands.Count)
                                    break;
                                andGroupTotal += groupHours[j];
                                j++;
                                if (j >= operands.Count)
                                    break;
                            } while (operands[j] == 'A');

                            andGroups.Add(andGroupTotal);
                        }
			    
			float areaHours = 0;

                        if (andGroups.Count == 0)
                        {
                            areaHours += groupHours.Max();
                        }
                        else
                        {
                            areaHours += andGroups.Max();
                        }
			    
			totalProgramHours += areaHours;

                        if (genEdArea)
                        {
                            totalGenEdHours += areaHours;
                        }
                        else
                        {
                            totalCoreAndProfessionalHours += areaHours;
                        }
                    }

                    TotalProgramHoursForProgram[key] += totalProgramHours;
                    TotalGeneralEducationHoursForProgram[key] += totalGenEdHours;
                    TotalCoreAndProfessionalForProgram[key] += totalCoreAndProfessionalHours;
                }
            }

            DirectoryInfo dataDirectory = new DirectoryInfo("..\\..\\..\\data\\" + options.month + " " + options.year);

            if (!dataDirectory.Exists)
            {
                Directory.CreateDirectory("..\\..\\..\\data\\" + options.month + " " + options.year);
            }

            using (System.IO.StreamWriter file = new System.IO.StreamWriter("..\\..\\..\\data\\" + options.month + " " + options.year + 
                "\\SACS Course Delivery Method Report " + options.month + " " + options.year + ".csv"))
            {
                file.WriteLine("Begin Term, End Term, Award Type, POS Code, POS Title, PGM Hrs (reqd for degree), # PGM hrs via DE, % PGM Hrs via DE, # Gen Ed Hrs via DE, % Gen Ed Hrs via DE, # Prof Hrs via DE, % Prof Hrs via DE, FA Apprvd");

                foreach (AcademicProgram prog in programs)
                {
                    foreach (AcademicProgram.CatalogChange catalog in prog.catalogChanges)
                    {
                        Tuple<String, String> key = new Tuple<string,string>(prog.progCode, catalog.effectiveTerm);


                        float totalProgramHours = TotalProgramHoursForProgram[key] > catalog.totalProgramHours ? catalog.totalProgramHours : TotalProgramHoursForProgram[key];
                        float totalGenEdHours = TotalGeneralEducationHoursForProgram[key] > catalog.totalGeneralEducationHours ? catalog.totalGeneralEducationHours : TotalGeneralEducationHoursForProgram[key];
                        float totalCoreAndProfessional = TotalCoreAndProfessionalForProgram[key] > catalog.totalCoreAndProfessionalHours ? catalog.totalCoreAndProfessionalHours : TotalCoreAndProfessionalForProgram[key];

                        float percentProgramHours = totalProgramHours / catalog.totalProgramHours;
                        float percentGenEdHours = (float)(catalog.totalGeneralEducationHours == 0 ? 0.00 : (totalGenEdHours / catalog.totalGeneralEducationHours));
                        float percentCoreAndProfessionalHours = (float)(catalog.totalCoreAndProfessionalHours == 0 ? 0.00 : (totalCoreAndProfessional / catalog.totalCoreAndProfessionalHours));

                        percentProgramHours = percentProgramHours * 100;
                        percentGenEdHours = percentGenEdHours * 100;
                        percentCoreAndProfessionalHours = percentCoreAndProfessionalHours * 100;

                        file.WriteLine(String.Format(options.minTerm + "," + options.maxTerm + "," + prog.awardType + "," + prog.progCode + @",""" + prog.progName + @"""," + catalog.totalProgramHours + ","
                            + totalProgramHours + ",{0:0.00}," + totalGenEdHours + ",{1:0.00}," + totalCoreAndProfessional + ",{2:0.00}," + prog.financialAidApproved, percentProgramHours, percentGenEdHours, percentCoreAndProfessionalHours));
                    }
                }

                file.Close();
            }
        }
    }
}
