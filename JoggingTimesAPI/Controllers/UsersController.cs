using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JoggingTimesAPI;
using JoggingTimesAPI.Models;
using JoggingTimesAPI.Services;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using AutoMapper;
using JoggingTimesAPI.Helpers;
using Microsoft.Extensions.Options;

namespace JoggingTimesAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private IUserService _userService;
        private IMapper _mapper;
        private readonly AppSettings _appSettings;
        private User _authenticatedUser;

        public User AuthenticatedUser
        {
            get
            {
                if (User != null && _authenticatedUser == null)
                {
                    _authenticatedUser = new User
                    {
                        Username = User.Identity.Name,
                        Role = Enum.Parse<UserRole>(User.Claims.Single(c => c.Type == ClaimTypes.Role).Value)
                    };
                }
                return _authenticatedUser;
            }
            set
            {
                if (User != null)
                    throw new ApplicationException("Cannot change Authenticated User, property is read-only.");
                _authenticatedUser = value;
            }
        }

        public UsersController(
            IUserService userService,
            IMapper mapper,
            IOptions<AppSettings> appSettings)
        {
            _userService = userService;
            _mapper = mapper;
            _appSettings = appSettings.Value;
        }

        [AllowAnonymous]
        [HttpPost("authenticate")]
        public async Task<IActionResult> Authenticate(UserAuthenticateModel model)
        {
            var user = await _userService.Authenticate(model.Username, model.Password);

            if (user == null)
                return BadRequest(new { message = "Invalid username and/or password." });
            
            var tokenString = GetAuthToken(user.Username, user.Role);

            return Ok(new
            {
                user.Username,
                user.Role,
                Token = tokenString
            });
        }

        [AllowAnonymous]
        [HttpPut("register")]
        public async Task<ActionResult<User>> Register(UserRegisterModel model)
        {
            var user = _mapper.Map<User>(model);
            user.NewPassword = model.Password;

            try
            {
                user = await _userService.Create(user);
                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("update")]
        public async Task<IActionResult> Update(UserUpdateModel model)
        {
            var user = _mapper.Map<User>(model);
            user.NewPassword = model.Password;

            try
            {
                await _userService.Update(user, AuthenticatedUser);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private string GetAuthToken(string userName, UserRole role)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_appSettings.Secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, userName),
                    new Claim(ClaimTypes.Role, role.ToString())
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);
            return tokenString;
        }
    }
}
