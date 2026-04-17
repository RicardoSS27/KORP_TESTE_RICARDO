# Sistema de Notas Fiscais

## Descrição Geral

Este projeto consiste em uma solução para cadastro de produtos, emissão de notas fiscais e baixa automática de estoque no momento da impressão da nota. A aplicação foi desenvolvida com arquitetura distribuída, separando as responsabilidades entre frontend e backend por meio de microsserviços.

O objetivo da solução é permitir que o usuário:

- cadastre produtos com código, descrição e saldo inicial;
- consulte os produtos disponíveis em estoque;
- crie notas fiscais com um ou mais itens;
- visualize as notas já registradas;
- imprima notas fiscais abertas;
- realize a baixa de estoque de forma integrada no momento do fechamento da nota.

## Funcionalidades Implementadas

### Frontend

No frontend, foi desenvolvida uma interface web para operação centralizada do sistema. A aplicação permite:

- cadastrar produtos com código, descrição e saldo inicial;
- listar os produtos cadastrados e seus respectivos saldos;
- montar notas fiscais com múltiplos itens;
- selecionar produtos existentes para composição da nota;
- informar a quantidade de cada item;
- listar as notas fiscais já criadas;
- identificar o status de cada nota, aberta ou fechada;
- imprimir notas abertas;
- atualizar dinamicamente os dados exibidos na tela;
- visualizar indicadores resumidos, como total de produtos, total de notas e total de notas em aberto.

### Backend

No backend, a solução foi estruturada em dois microsserviços:

- `estoque-service`: responsável pelo cadastro de produtos, listagem de produtos e baixa de estoque;
- `faturamento-service`: responsável pela criação, listagem e impressão de notas fiscais.

Durante a impressão da nota fiscal, o serviço de faturamento realiza chamadas HTTP ao serviço de estoque para efetuar a baixa dos itens informados. Após a atualização do estoque, a nota é marcada como fechada.

## Arquitetura da Solução

O projeto foi organizado da seguinte forma:

- `frontend`: aplicação Angular responsável pela interface do usuário;
- `services/estoque-service`: API de estoque;
- `services/faturamento-service`: API de faturamento;
- `sqlserver`: banco de dados relacional executado em contêiner Docker;
- `docker-compose.yml`: orquestração de todos os serviços da solução.

O ambiente é inicializado com Docker Compose, possibilitando subir frontend, microsserviços e banco de dados de forma integrada.

## Tecnologias Utilizadas

### Frontend

- Angular 21
- TypeScript
- RxJS
- HTML
- CSS

### Backend

- C#
- .NET 8
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- Polly
- Swagger

### Infraestrutura

- Docker
- Docker Compose

## Detalhamento Técnico

### Ciclos de Vida do Angular Utilizados

Foi utilizado o ciclo de vida `OnInit`, implementado por meio do método `ngOnInit()`.

Esse método é responsável por:

- adicionar o primeiro item da nota fiscal ao carregar a tela;
- realizar o carregamento inicial do dashboard;
- buscar, logo na inicialização da aplicação, as listas de produtos e notas fiscais.

### Uso de RxJS

Sim, houve uso da biblioteca RxJS.

O RxJS foi utilizado principalmente no consumo assíncrono das APIs, com destaque para:

- `Observable`: utilizado como tipo de retorno das chamadas HTTP no serviço `ApiService`;
- `forkJoin`: utilizado para carregar simultaneamente produtos e notas fiscais;
- `finalize`: utilizado para encerrar estados de carregamento após operações de criação e impressão;
- `subscribe`: utilizado para tratar respostas de sucesso e erro das chamadas aos serviços.

Esse uso permitiu organizar o fluxo assíncrono da aplicação e controlar melhor o estado visual da interface durante as operações.

### Outras Bibliotecas Utilizadas e Finalidade

No frontend, além do RxJS, foram utilizadas bibliotecas do ecossistema Angular:

- `@angular/common`: diretivas e recursos comuns da aplicação;
- `@angular/forms`: binding de formulários com `ngModel`;
- `@angular/common/http`: comunicação HTTP com o backend;
- `@angular/platform-browser`: inicialização da aplicação no navegador;
- `zone.js`: suporte ao mecanismo de detecção de mudanças;
- `tslib`: biblioteca de suporte ao código gerado pelo TypeScript.

No backend, foram utilizadas:

- `Microsoft.EntityFrameworkCore.SqlServer`: integração do Entity Framework Core com SQL Server;
- `Microsoft.EntityFrameworkCore.Tools`: suporte a ferramentas do EF Core;
- `Swashbuckle.AspNetCore`: geração da documentação Swagger;
- `Microsoft.Extensions.Http.Polly`: integração do Polly com `HttpClient`;
- `Polly`: política de retry para maior resiliência na comunicação entre microsserviços.

### Bibliotecas Utilizadas para Componentes Visuais

A interface foi construída com:

- HTML nativo;
- CSS customizado;
- componentes standalone do Angular;
- diretivas e recursos nativos do framework.

### Frameworks Utilizados no C#

Os principais frameworks e componentes utilizados no backend em C# foram:

- `ASP.NET Core Web API`: construção e exposição dos endpoints REST;
- `Entity Framework Core`: mapeamento objeto-relacional e acesso a dados;
- `Entity Framework Core for SQL Server`: persistência em banco SQL Server.

### Tratamento de Erros e Exceções no Backend

O tratamento de erros e exceções foi implementado com foco em regras de negócio e resiliência de infraestrutura.

Foram adotadas as seguintes abordagens:

- retorno de `BadRequest` em situações como estoque insuficiente;
- retorno de `BadRequest` para tentativa de impressão de nota já fechada;
- retorno de `NotFound` quando a nota fiscal não é localizada;
- retorno de `502 Bad Gateway` quando ocorre falha na integração com o serviço de estoque;
- captura de `SqlException` durante a inicialização dos bancos;
- nova tentativa automática de conexão com o banco em caso de indisponibilidade temporária;
- registro de logs de advertência durante falhas transitórias de inicialização;
- lançamento de `InvalidOperationException` caso o banco não possa ser inicializado após o número máximo de tentativas.

Além disso, no serviço de faturamento foi utilizada uma política de retry com Polly para chamadas HTTP ao serviço de estoque, tratando falhas transitórias de comunicação.

### Uso de LINQ no C#

Sim, houve uso de LINQ, especialmente nas consultas realizadas com Entity Framework Core.

Os principais usos observados foram:

- `FirstOrDefaultAsync(...)`: busca de nota fiscal por identificador;
- `Include(...)`: carregamento dos itens relacionados a cada nota fiscal;
- `ToListAsync()`: materialização das listas retornadas pelas APIs.

O LINQ foi utilizado como mecanismo de consulta sobre as entidades persistidas, facilitando a leitura do código e a integração com o ORM.

## Persistência de Dados

Os dados são armazenados em SQL Server, com bancos independentes para os dois microsserviços:

- `EstoqueDb`
- `FaturamentoDb`

Ambos são criados automaticamente durante a inicialização da aplicação, caso ainda não existam. Também há uma verificação para criação das tabelas necessárias.

## Resiliência e Integração

A solução adota práticas de resiliência para tornar a inicialização e a comunicação entre serviços mais robustas:

- criação automática dos bancos de dados;
- tentativas de reconexão em caso de indisponibilidade do SQL Server;
- comunicação HTTP entre microsserviços;
- política de retry com Polly no consumo do serviço de estoque;
- separação de responsabilidades entre estoque e faturamento.

## Como Executar o Projeto

### Pré-requisitos

- Docker
- Docker Compose

### Execução

Na raiz do projeto, execute:

```bash
docker-compose up --build
```

### Endpoints e Acessos

- Frontend: [http://localhost:4200](http://localhost:4200)
- Swagger do estoque: [http://localhost:5001/swagger](http://localhost:5001/swagger)
- Swagger do faturamento: [http://localhost:5002/swagger](http://localhost:5002/swagger)

## Fluxo Básico de Uso

1. Cadastrar um ou mais produtos no sistema.
2. Criar uma nota fiscal com os produtos cadastrados.
3. Listar as notas disponíveis.
4. Imprimir uma nota aberta.
5. Confirmar a baixa automática do estoque e o fechamento da nota.

## Considerações Finais

A solução entregue contempla uma arquitetura modular e funcional, com separação entre interface, estoque e faturamento. O projeto combina frontend em Angular com backend em C# e .NET, persistência em SQL Server, documentação via Swagger e execução containerizada com Docker Compose.

Trata-se de uma implementação adequada para demonstrar integração entre microsserviços, operações de cadastro e processamento de notas fiscais, além de práticas de resiliência e organização da solução em camadas bem definidas.
