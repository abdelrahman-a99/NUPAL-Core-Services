using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUPAL.Core.Application.DTOs;
using NUPAL.Core.Application.Interfaces;

namespace NUPAL.Core.Infrastructure.Services
{
    public class WuzzufJobService : IJobService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WuzzufJobService> _logger;

        public WuzzufJobService(HttpClient httpClient, ILogger<WuzzufJobService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            
            // Set user agent to avoid being blocked
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public async Task<IEnumerable<JobDto>> GetJobsAsync(string? what = null, string? where = null, string? country = null)
        {
            try
            {
                // Use defaults if not provided
                var keyword = string.IsNullOrWhiteSpace(what) ? "software" : what;
                var location = where; // Allow null/empty for "All" locations

                _logger.LogInformation("Scraping jobs from Wuzzuf: Keyword={Keyword}, Location={Location}", keyword, location ?? "All");

                var allJobs = new List<JobDto>();
                
                // Scrape multiple pages to get more results (Wuzzuf shows ~15 jobs per page)
                // We'll fetch 5 pages to get around 50-75 jobs
                var pagesToFetch = 5;
                var tasks = new List<Task<IEnumerable<JobDto>>>();

                for (int page = 0; page < pagesToFetch; page++)
                {
                    tasks.Add(FetchJobsFromPage(keyword, location, page));
                }

                // Wait for all pages to be fetched
                var results = await Task.WhenAll(tasks);
                
                // Combine all results
                foreach (var pageJobs in results)
                {
                    allJobs.AddRange(pageJobs);
                }

                // Remove duplicates based on job ID
                var uniqueJobs = allJobs
                    .GroupBy(j => j.Id)
                    .Select(g => g.First())
                    .ToList();
                
                _logger.LogInformation("Successfully scraped {Count} unique jobs from {Pages} pages", uniqueJobs.Count, pagesToFetch);
                
                return uniqueJobs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping jobs from Wuzzuf");
                throw;
            }
        }

        private async Task<IEnumerable<JobDto>> FetchJobsFromPage(string keyword, string? location, int pageNumber)
        {
            try
            {
                // Build Wuzzuf search URL with location and pagination
                var encodedKeyword = HttpUtility.UrlEncode(keyword);
                var start = pageNumber * 15; // Wuzzuf uses 15 jobs per page
                
                // Only add location filter if a location is provided
                var locationFilter = "";
                if (!string.IsNullOrWhiteSpace(location))
                {
                    var encodedLocation = HttpUtility.UrlEncode(location);
                    locationFilter = $"&filters[location][0]={encodedLocation}";
                }
                
                var url = $"https://wuzzuf.net/search/jobs/?q={encodedKeyword}&a=hpb{locationFilter}&start={start}";

                _logger.LogInformation("Fetching page {Page} from Wuzzuf (start={Start})", pageNumber + 1, start);

                // Add delay to avoid rate limiting (stagger requests)
                await Task.Delay(pageNumber * 500); // Stagger by 500ms per page

                var html = await _httpClient.GetStringAsync(url);
                
                var jobs = ParseWuzzufJobsFromJson(html, location);
                
                _logger.LogInformation("Page {Page}: Found {Count} jobs", pageNumber + 1, jobs.Count());
                
                return jobs;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching page {Page}: {Message}", pageNumber + 1, ex.Message);
                return Enumerable.Empty<JobDto>();
            }
        }

        private IEnumerable<JobDto> ParseWuzzufJobsFromJson(string html, string locationFilter)
        {
            var jobs = new List<JobDto>();

            try
            {
                // Extract embedded JSON from page using regex
                var match = Regex.Match(html, @"Wuzzuf\.initialStoreState\s*=\s*(\{.*?\});", RegexOptions.Singleline);
                
                if (!match.Success)
                {
                    _logger.LogWarning("Could not find Wuzzuf.initialStoreState in page HTML");
                    return jobs;
                }

                var jsonText = match.Groups[1].Value;
                _logger.LogInformation("Extracted JSON, length: {Length} chars", jsonText.Length);
                
                var root = JObject.Parse(jsonText);

                // Navigate to jobs collection
                var jobsCollection = root["entities"]?["job"]?["collection"];
                
                if (jobsCollection == null)
                {
                    _logger.LogWarning("Jobs collection not found in JSON");
                    return jobs;
                }

                // Get company entities for lookup
                var companyEntities = root["entities"]?["company"]?["collection"];

                var totalJobs = jobsCollection.Children<JProperty>().Count();
                _logger.LogInformation("Found {TotalJobs} jobs in collection", totalJobs);

                int index = 0;
                foreach (var jobEntry in jobsCollection.Children<JProperty>())
                {
                    index++;
                    try
                    {
                        var jobToken = jobEntry.Value;
                        if (jobToken == null) continue;

                        var jobData = jobToken["attributes"];
                        if (jobData == null)
                        {
                            _logger.LogWarning("Job {Index}: attributes is null, skipping", index);
                            continue;
                        }

                        // Extract basic fields
                        var title = jobData["title"]?.ToString() ?? "Unknown Title";
                        var slug = jobData["uri"]?.ToString() ?? "";
                        var createdDate = jobData["postedAt"]?.ToString() ?? DateTime.Now.ToString("yyyy-MM-dd");
                        
                        // Extract and decode description (contains HTML entities)
                        var descToken = jobData["description"];
                        var snippetToken = jobData["snippet"];
                        var rawDescription = descToken?.ToString() 
                            ?? snippetToken?.ToString() 
                            ?? "No description available";
                        
                        // Decode HTML entities like &amp; &lt; &gt; etc.
                        var description = System.Net.WebUtility.HtmlDecode(rawDescription);
                        
                        // Remove HTML tags
                        description = System.Text.RegularExpressions.Regex.Replace(description, "<.*?>", " ");
                        description = System.Text.RegularExpressions.Regex.Replace(description, @"\s+", " ").Trim();

                        // Extract employment type from workTypes array (e.g. full_time)
                        // Fallback to jobType field (usually "job") or default
                        string jobType = "full_time";
                        var workTypesToken = jobData["workTypes"];
                        
                        if (workTypesToken != null && workTypesToken.Type == JTokenType.Array && workTypesToken.Any())
                        {
                            jobType = workTypesToken.First()?["name"]?.ToString() ?? "full_time";
                        }
                        else
                        {
                            jobType = jobData["jobType"]?.ToString() ?? "full_time";
                        }
                        
                        // Extract workplace type (Remote, Hybrid, On-site)
                        var workplaceType = jobData["workplaceType"]?.ToString();
                        
                        if (string.IsNullOrEmpty(workplaceType))
                        {
                            // Try workplaceArrangement object (common in recent Wuzzuf JSON)
                            var workplaceArrangement = jobData["workplaceArrangement"];
                            if (workplaceArrangement != null && workplaceArrangement.Type == JTokenType.Object)
                            {
                                workplaceType = workplaceArrangement["displayedName"]?.ToString();
                            }
                        }
                        
                        if (string.IsNullOrEmpty(workplaceType))
                        {
                            // Try alternative field names
                            workplaceType = jobData["workType"]?.ToString();
                        }
                        if (string.IsNullOrEmpty(workplaceType))
                        {
                            workplaceType = jobData["workLocation"]?.ToString();
                        }

                        // Extract company name from relationships
                        string companyName = "Unknown Company";
                        var relationships = jobToken["relationships"];
                        if (relationships != null && relationships.Type == JTokenType.Object && companyEntities != null)
                        {
                            var companyRelation = relationships["company"]?["data"];
                            if (companyRelation != null && companyRelation.Type == JTokenType.Object)
                            {
                                var companyId = companyRelation["id"]?.ToString();
                                if (!string.IsNullOrEmpty(companyId))
                                {
                                    var company = companyEntities[companyId];
                                    if (company != null)
                                    {
                                        companyName = company["attributes"]?["name"]?.ToString() ?? "Unknown Company";
                                    }
                                }
                            }
                        }

                        // Extract location from job data (not from filter)
                        string cityName = "Egypt"; // Default
                        var locationToken = jobData["location"];
                        
                        if (locationToken != null && locationToken.Type == JTokenType.Object)
                        {
                            // Location is an object with "name" field
                            cityName = locationToken["name"]?.ToString() ?? "Egypt";
                        }
                        else if (locationToken != null && locationToken.Type == JTokenType.String)
                        {
                            cityName = locationToken.ToString();
                        }
                        else if (locationToken != null && locationToken.Type == JTokenType.Array && locationToken.Any())
                        {
                            // Sometimes location is an array
                            var firstLocation = locationToken.First();
                            if (firstLocation.Type == JTokenType.Object)
                            {
                                cityName = firstLocation["name"]?.ToString() ?? "Egypt";
                            }
                            else
                            {
                                cityName = firstLocation.ToString();
                            }
                        }

                        // Build job URL
                        var jobUrl = !string.IsNullOrWhiteSpace(slug) 
                            ? $"https://wuzzuf.net/{slug}" 
                            : "#";

                        // Extract more accurate location from URL slug as fallback
                        // URL format: jobs/p/xxx-job-title-company-CITY-egypt
                        if (!string.IsNullOrWhiteSpace(slug))
                        {
                            var urlParts = slug.Split('-');
                            // Look for city name before "egypt" in the URL
                            for (int i = urlParts.Length - 1; i >= 0; i--)
                            {
                                if (urlParts[i].Equals("egypt", StringComparison.OrdinalIgnoreCase) && i > 0)
                                {
                                    var potentialCity = urlParts[i - 1];
                                    // Capitalize first letter
                                    if (!string.IsNullOrWhiteSpace(potentialCity))
                                    {
                                        cityName = char.ToUpper(potentialCity[0]) + potentialCity.Substring(1);
                                        break;
                                    }
                                }
                            }
                        }

                        // Extract work type from workplaceType field or description/title
                        string workType = "on-site"; // Default
                        
                        if (!string.IsNullOrEmpty(workplaceType))
                        {
                            // Use the workplaceType from Wuzzuf data
                            var normalizedType = workplaceType.ToLower().Trim();
                            
                            if (normalizedType.Contains("remote") || normalizedType == "remote")
                            {
                                workType = "remote";
                            }
                            else if (normalizedType.Contains("hybrid") || normalizedType == "hybrid")
                            {
                                workType = "hybrid";
                            }
                            else if (normalizedType.Contains("on-site") || normalizedType.Contains("onsite") || normalizedType == "on-site")
                            {
                                workType = "on-site";
                            }
                        }
                        else
                        {
                            // Fallback: Extract from description or title
                            var combinedText = (title + " " + description).ToLower();
                            
                            if (combinedText.Contains("remote") || combinedText.Contains("work from home") || combinedText.Contains("wfh"))
                            {
                                workType = "remote";
                            }
                            else if (combinedText.Contains("hybrid"))
                            {
                                workType = "hybrid";
                            }
                        }

                        var job = new JobDto
                        {
                            Id = jobEntry.Name,
                            Title = title,
                            CompanyName = companyName,
                            Location = cityName,
                            Description = description,
                            RedirectUrl = jobUrl,
                            Created = createdDate,
                            Category = "IT Jobs",
                            ContractTime = jobType,
                            WorkType = workType,
                            SalaryMin = null,
                            SalaryMax = null
                        };

                        jobs.Add(job);
                        _logger.LogDebug("Job {Index}: Successfully parsed '{Title}' at {Company}", index, title, companyName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Job {Index}: Failed to parse - {Message}", index, ex.Message);
                        continue;
                    }
                }

                _logger.LogInformation("Successfully parsed {Count}/{Total} jobs from Wuzzuf", jobs.Count, totalJobs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error parsing Wuzzuf JSON: {Message}", ex.Message);
            }

            return jobs;
        }
    }
}
