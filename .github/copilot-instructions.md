# Copilot Instructions for ProxyServer Project

## Project Overview

This is a **Caching Proxy Server** written in C# using ASP.NET Core. The project implements a high-performance HTTP proxy with built-in caching capabilities, access control, and authentication.
Main proxy client is ollama. So if you want to expose local ollama server,
for example if your home pc is powerful enough to run ollama models,
and you want to be able to use it from your phone or notebook -
it's useful to run this proxy server on your home pc.

Also it's useful in case of ollama related code debugging,
is ollama server usually not so fast - caching responses give you ability to dubug your code much faster. 

## Key Features

- **HTTP Proxy**: Forward requests to upstream servers
- **Response Caching**: Cache responses with configurable TTL
- **Access Control**: IP-based filtering and Basic Authentication
- **Performance Monitoring**: Track cache hit rates and response times
- **Configurable Settings**: JSON-based configuration

## Coding Standards

- **Language**: All code, comments, and documentation should be in **English**
- **File Structure**: Follow C# conventions - one class per file
- **Naming**: Use PascalCase for classes, methods, properties; camelCase for local variables
- **Documentation**: XML documentation comments for public APIs
- **Testing**: Comprehensive unit and functional tests

