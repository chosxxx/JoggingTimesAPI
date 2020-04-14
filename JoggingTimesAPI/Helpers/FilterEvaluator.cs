#region Comments
/* **********************************************************************************
 * This Filter Evaluator was built based on Irony for a Toptal assessment project.
 * Statement of requirement: The API filtering should allow using parenthesis for 
 *      defining operations precedence and use any combination of the available fields. 
 *      The supported operations should at least include or, and, eq (equals), 
 *      ne (not equals), gt (greater than), lt (lower than).
 *      Example -> (date eq '2016-05-01') AND ((distance gt 20) OR (distance lt 10)
 * **********************************************************************************/
#endregion

using Irony.Interpreter.Evaluator;
using Irony.Parsing;
using JoggingTimesAPI.Entities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace JoggingTimesAPI.Helpers
{
    public interface IFilterEvaluator
    {
        Expression EvaluateLogKeySelector<T>(string key);
        Expression EvaluateUserKeySelector<T>(string key);
        Expression<Func<User, bool>> EvaluateUserFilterPredicate(string filter);
        Expression<Func<JoggingTimeLog, bool>> EvaluateLogFilterPredicate(string filter);
    }

    public class FilterEvaluator : IFilterEvaluator
    {
        #region private methods
        private string ConvertToCSharp(string expression)
        {
            var paramMapper = new Dictionary<string, string>
            {
                { "or", "||" },
                { "and", "&&" },
                { "eq", "==" },
                { "ne", "!=" },
                { "neq", "!=" },
                { "<>", "!=" },
                { "gt", ">" },
                { "gteq", ">=" },
                { "lt", "<" },
                { "lteq", "<=" }
                // Add more if needed
            };

            foreach (var p in paramMapper)
            {
                expression = Regex.Replace(expression, @$"\b{p.Key}\b", p.Value, RegexOptions.IgnoreCase);
            }

            return expression;
        }

        private ParseTree GetParseTree(string filter)
        {
            var grammar = new ExpressionEvaluatorGrammar();
            var parser = new Parser(grammar);
            return parser.Parse(ConvertToCSharp(filter));
        }

        private Expression GenerateIdentifierExpression<T>(ParseTreeNode node)
        {
            switch (node.Term.Name)
            {
                case "identifier":
                    {
                        var tokenVal = node.FindTokenAndGetText();
                        if (typeof(T) == typeof(User))
                        {
                            if (Enum.TryParse(tokenVal, ignoreCase: true, out UserRole roleVal))
                                return (Expression<Func<T, UserRole>>)(t => roleVal);

                            return EvaluateUserKeySelector<T>(tokenVal);
                        }
                        if (typeof(T) == typeof(JoggingTimeLog))
                            return EvaluateLogKeySelector<T>(tokenVal);

                        throw new ApplicationException($"Invalid type {typeof(T)}");
                    }
                case "number":
                    return (Expression<Func<T, double>>)(t => double.Parse(node.FindTokenAndGetText()));
                case "string":
                    {
                        // For some reason FindTokenAndGetText keeps quotes for String values
                        var tokenVal = node.FindTokenAndGetText().Replace("\"", "").Replace("'", "");

                        if (double.TryParse(tokenVal, out double doubleVal))
                            return (Expression<Func<T, double>>)(t => doubleVal);

                        if (DateTime.TryParse(tokenVal, out DateTime dateTimeVal))
                            return (Expression<Func<T, DateTime>>)(t => dateTimeVal);

                        return (Expression<Func<T, string>>)(t => tokenVal);
                    }
                default:
                    {
                        var tokenVal = node.FindTokenAndGetText();
                        throw new ApplicationException($"Invalid token: {tokenVal}");
                    }
            }
        }

        private Expression<Func<T, bool>> GenerateBinaryExpression<T>(ParseTreeNode node)
        {
            if (node.ChildNodes.Count != 3)
                throw new ApplicationException($"Invalid filter '{node.FindTokenAndGetText()}'");

            var parameter = Expression.Parameter(typeof(T));
            var leftNode = node.ChildNodes[0];
            var tokenVal = node.ChildNodes[1].FindTokenAndGetText();
            var rightNode = node.ChildNodes[2];

            Expression leftExpression = leftNode.Term.Name.Equals("BinExpr") ?
                Expression.Invoke(GenerateBinaryExpression<T>(leftNode), parameter) :
                Expression.Invoke(GenerateIdentifierExpression<T>(leftNode), parameter);
            Expression rightExpression = rightNode.Term.Name.Equals("BinExpr") ?
                Expression.Invoke(GenerateBinaryExpression<T>(rightNode), parameter) :
                Expression.Invoke(GenerateIdentifierExpression<T>(rightNode), parameter);

            Expression expression;
            switch (tokenVal)
            {
                case "==":
                    expression = Expression.Equal(leftExpression, rightExpression);
                    break;
                case "!=":
                    expression = Expression.NotEqual(leftExpression, rightExpression);
                    break;
                case ">":
                    expression = Expression.GreaterThan(leftExpression, rightExpression);
                    break;
                case ">=":
                    expression = Expression.GreaterThanOrEqual(leftExpression, rightExpression);
                    break;
                case "<":
                    expression = Expression.LessThan(leftExpression, rightExpression);
                    break;
                case "<=":
                    expression = Expression.LessThanOrEqual(leftExpression, rightExpression);
                    break;
                case "&&":
                    expression = Expression.AndAlso(leftExpression, rightExpression);
                    break;
                case "||":
                    expression = Expression.OrElse(leftExpression, rightExpression);
                    break;
                default:
                    throw new ApplicationException($"Invalid token {tokenVal}");
            }
            return (Expression<Func<T, bool>>)Expression.Lambda(expression, parameter);
        }
        #endregion

        public Expression<Func<User, bool>> EvaluateUserFilterPredicate(string filter)
        {
            var parserTree = GetParseTree(filter);
            return GenerateBinaryExpression<User>(parserTree.Root.ChildNodes[0]);
        }

        public Expression<Func<JoggingTimeLog, bool>> EvaluateLogFilterPredicate(string filter)
        {
            var parserTree = GetParseTree(filter);

            return GenerateBinaryExpression<JoggingTimeLog>(parserTree.Root.ChildNodes[0]);
        }

        public Expression EvaluateLogKeySelector<T>(string key)
        {
            return key.ToLower() switch
            {
                "joggingtimelogid" => (Expression<Func<T, int>>)(t => ((JoggingTimeLog)Convert.ChangeType(t, typeof(JoggingTimeLog))).JoggingTimeLogId),
                "username" => (Expression<Func<T, string>>)(t => ((JoggingTimeLog)Convert.ChangeType(t, typeof(JoggingTimeLog))).UserName),
                "startdatetime" => (Expression<Func<T, DateTime>>)(t => ((JoggingTimeLog)Convert.ChangeType(t, typeof(JoggingTimeLog))).StartDateTime),
                "updateddatetime" => (Expression<Func<T, DateTime>>)(t => ((JoggingTimeLog)Convert.ChangeType(t, typeof(JoggingTimeLog))).UpdatedDateTime),
                "distancemeters" => (Expression<Func<T, double>>)(t => ((JoggingTimeLog)Convert.ChangeType(t, typeof(JoggingTimeLog))).DistanceMetres),
                "latitude" => (Expression<Func<T, double>>)(t => ((JoggingTimeLog)Convert.ChangeType(t, typeof(JoggingTimeLog))).Latitude),
                "longitude" => (Expression<Func<T, double>>)(t => ((JoggingTimeLog)Convert.ChangeType(t, typeof(JoggingTimeLog))).Longitude),
                "active" => (Expression<Func<T, bool>>)(t => ((JoggingTimeLog)Convert.ChangeType(t, typeof(JoggingTimeLog))).Active),
                _ => throw new ApplicationException($"Invalid token: {key}")
            };
        }

        public Expression EvaluateUserKeySelector<T>(string key)
        {
            return key.ToLower() switch
            {
                "username" => (Expression<Func<T, string>>)(t => ((User)Convert.ChangeType(t, typeof(User))).Username),
                "emailaddress" => (Expression<Func<T, string>>)(t => ((User)Convert.ChangeType(t, typeof(User))).EmailAddress),
                "role" => (Expression<Func<T, UserRole>>)(t => ((User)Convert.ChangeType(t, typeof(User))).Role),
                _ => throw new ApplicationException($"Invalid token: {key}")
            };
        }
    }
}
